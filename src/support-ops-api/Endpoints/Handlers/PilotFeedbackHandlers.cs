using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SupportOpsApi.Data;
using SupportOpsApi.Models;
using Continuo.Observability.Attributes;

namespace SupportOpsApi.Endpoints.Handlers;

public static class PilotFeedbackHandlers {
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase) {
        "new", "triaged", "resolved", "dismissed"
    };

    [ContinuoProxyMethod("ui")]
    public static async Task<Ok<PilotFeedbackListResponse>> ListFeedback(
        SupportOpsDbContext db,
        string? pilotSite,
        string? status,
        string? category,
        int? take,
        CancellationToken ct) {
        var query = db.PilotFeedbacks.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(pilotSite)) {
            query = query.Where(x => x.PilotSite == pilotSite);
        }
        if (!string.IsNullOrWhiteSpace(status)) {
            query = query.Where(x => x.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(category)) {
            query = query.Where(x => x.Category == category);
        }
        var capped = Math.Clamp(take ?? 100, 1, 500);
        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(capped)
            .ToListAsync(ct);

        var summary = await db.PilotFeedbacks
            .AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var averageSatisfaction = await db.PilotFeedbacks
            .AsNoTracking()
            .Where(x => x.Satisfaction >= 1 && x.Satisfaction <= 5)
            .Select(x => (double?)x.Satisfaction)
            .AverageAsync(ct) ?? 0;

        return TypedResults.Ok(new PilotFeedbackListResponse(
            items,
            summary.ToDictionary(s => s.Status, s => s.Count, StringComparer.OrdinalIgnoreCase),
            Math.Round(averageSatisfaction, 2)));
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<PilotFeedback>, NotFound>> GetFeedback(
        SupportOpsDbContext db, Guid id, CancellationToken ct) {
        var row = await db.PilotFeedbacks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? TypedResults.NotFound() : TypedResults.Ok(row);
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<PilotFeedback>, BadRequest<string>>> CreateFeedback(
        PilotFeedbackCreateRequest request,
        SupportOpsDbContext db,
        CancellationToken ct) {
        var pilotSite = (request.PilotSite ?? string.Empty).Trim();
        var title = (request.Title ?? string.Empty).Trim();
        var message = (request.Message ?? string.Empty).Trim();
        var category = string.IsNullOrWhiteSpace(request.Category) ? "general" : request.Category.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(pilotSite)) {
            return TypedResults.BadRequest("PilotSite is required");
        }
        if (string.IsNullOrEmpty(title)) {
            return TypedResults.BadRequest("Title is required");
        }
        if (string.IsNullOrEmpty(message)) {
            return TypedResults.BadRequest("Message is required");
        }
        var severity = Math.Clamp(request.Severity ?? 3, 1, 5);
        var satisfaction = Math.Clamp(request.Satisfaction ?? 3, 1, 5);

        var row = new PilotFeedback {
            PilotSite = pilotSite,
            Category = category,
            Severity = severity,
            Satisfaction = satisfaction,
            Title = title,
            Message = message,
            SubmittedBy = request.SubmittedBy?.Trim(),
            SubmittedRole = request.SubmittedRole?.Trim(),
            ContactEmail = request.ContactEmail?.Trim(),
            Status = "new",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        db.PilotFeedbacks.Add(row);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(row);
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<PilotFeedback>, NotFound, BadRequest<string>>> UpdateFeedback(
        Guid id,
        PilotFeedbackUpdateRequest request,
        SupportOpsDbContext db,
        CancellationToken ct) {
        var row = await db.PilotFeedbacks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) {
            return TypedResults.NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Status)) {
            var next = request.Status.Trim().ToLowerInvariant();
            if (!ValidStatuses.Contains(next)) {
                return TypedResults.BadRequest($"Status must be one of: {string.Join(", ", ValidStatuses)}");
            }
            row.Status = next;
            if (next is "resolved" or "dismissed") {
                row.ResolvedAtUtc = DateTimeOffset.UtcNow;
            } else if (next == "new") {
                row.ResolvedAtUtc = null;
            }
        }
        if (request.Resolution is not null) {
            row.Resolution = string.IsNullOrWhiteSpace(request.Resolution) ? null : request.Resolution.Trim();
        }
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(row);
    }
}

public record PilotFeedbackCreateRequest(
    string PilotSite,
    string? Category,
    int? Severity,
    int? Satisfaction,
    string Title,
    string Message,
    string? SubmittedBy,
    string? SubmittedRole,
    string? ContactEmail);

public record PilotFeedbackUpdateRequest(string? Status, string? Resolution);

public record PilotFeedbackListResponse(
    IReadOnlyList<PilotFeedback> Items,
    IReadOnlyDictionary<string, int> CountsByStatus,
    double AverageSatisfaction);
