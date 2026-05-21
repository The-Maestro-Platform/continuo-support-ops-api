using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SupportOpsApi.Data;
using SupportOpsApi.Models;
using SupportOpsApi.Services;
using Continuo.Observability.Attributes;

namespace SupportOpsApi.Endpoints.Handlers;

public static class SupportHandlers {
    [ContinuoProxyMethod("ui")]
    public static async Task<Ok<IEnumerable<Incident>>> GetIncidents(SupportOpsDbContext db, string? status, CancellationToken ct) {
        var query = db.Incidents.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) {
            query = query.Where(x => x.Status == status);
        }

        var incidents = await query
            .OrderByDescending(x => x.SlaMinutes)
            .Include(x => x.Actions)
            .ToListAsync(ct);
        return TypedResults.Ok(incidents.AsEnumerable());
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<SupportIntakeResponse>, BadRequest<string>, ContentHttpResult>> CreateSupportIntake(
        SupportIntakeRequest request,
        SupportOpsDbContext supportDb,
        TaskFlowDbContext taskDb,
        SlaPolicyService slaPolicy,
        IMemoryCache cache,
        HttpContext httpContext,
        CancellationToken ct) {
        var topic = (request.Topic ?? string.Empty).Trim();
        var summary = (request.Summary ?? string.Empty).Trim();
        var branch = (request.Branch ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(topic)) {
            return TypedResults.BadRequest("Topic is required");
        }

        if (string.IsNullOrWhiteSpace(summary)) {
            return TypedResults.BadRequest("Summary is required");
        }

        var now = DateTimeOffset.UtcNow;
        var retryAfter = EnsureIntakeRateLimit(cache, httpContext, branch, now);
        if (retryAfter.HasValue) {
            httpContext.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.Value.TotalSeconds)).ToString();
            var retryMinutes = Math.Max(1, (int)Math.Ceiling(retryAfter.Value.TotalMinutes));
            return TypedResults.Text(
                $"Support intake limit reached. Try again in {retryMinutes} minutes.",
                "text/plain",
                null,
                StatusCodes.Status429TooManyRequests);
        }

        var title = string.IsNullOrWhiteSpace(branch) ? topic : $"{topic} · {branch}";

        var incident = new Incident {
            Title = title,
            ExternalId = branch,
            Priority = "P3",
            Status = "Open",
            Owner = string.Empty,
            SlaMinutes = 240
        };

        supportDb.Incidents.Add(incident);
        supportDb.IncidentActions.Add(new IncidentAction {
            IncidentId = incident.Id,
            ActionType = "intake",
            Actor = "public-web",
            Notes = summary,
            CreatedAt = now
        });
        await supportDb.SaveChangesAsync(ct);

        var slaMinutes = await slaPolicy.ResolveSlaMinutesAsync("task", incident.Priority, ct);
        var slaTargetAt = slaMinutes > 0 ? now.AddMinutes(slaMinutes) : (DateTimeOffset?)null;

        var workItem = new WorkItem {
            Title = title,
            Type = "task",
            Status = "Backlog",
            Priority = incident.Priority,
            Assignee = string.Empty,
            Source = incident.Id.ToString(),
            ExternalId = incident.ExternalId,
            Tags = "public-web,incident",
            SlaMinutes = slaMinutes > 0 ? slaMinutes : null,
            SlaTargetAt = slaTargetAt,
            CreatedAt = now,
            UpdatedAt = now
        };

        var history = new WorkItemStatusChange {
            WorkItemId = workItem.Id,
            FromStatus = null,
            ToStatus = workItem.Status,
            Actor = "public-web",
            Note = "intake",
            ChangedAt = now
        };
        workItem.StatusHistory.Add(history);

        taskDb.WorkItems.Add(workItem);
        taskDb.WorkItemStatusChanges.Add(history);
        await taskDb.SaveChangesAsync(ct);

        return TypedResults.Ok(new SupportIntakeResponse(incident.Id, workItem.Id));
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<IncidentAction>, NotFound>> CreateIncidentAction(Guid id, IncidentAction input, SupportOpsDbContext db, CancellationToken ct) {
        var exists = await db.Incidents.AnyAsync(x => x.Id == id, ct);
        if (!exists) {
            return TypedResults.NotFound();
        }

        input.Id = Ulid.NewUlid().ToGuid();
        input.IncidentId = id;
        input.CreatedAt = DateTimeOffset.UtcNow;
        db.IncidentActions.Add(input);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(input);
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<Incident>, NotFound>> UpdateIncident(Guid id, UpdateIncidentRequest request, SupportOpsDbContext db, CancellationToken ct) {
        var incident = await db.Incidents.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (incident is null) {
            return TypedResults.NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Status)) {
            incident.Status = request.Status;
        }

        if (!string.IsNullOrWhiteSpace(request.Owner)) {
            incident.Owner = request.Owner;
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(incident);
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Ok<IEnumerable<Alert>>> GetAlerts(SupportOpsDbContext db, CancellationToken ct) {
        var alerts = await db.Alerts.OrderByDescending(x => x.RaisedAt).Take(50).ToListAsync(ct);
        return TypedResults.Ok(alerts.AsEnumerable());
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Ok<IEnumerable<KnowledgeArticle>>> GetKnowledgeBase(SupportOpsDbContext db, CancellationToken ct) {
        var docs = await db.KnowledgeArticles.Include(x => x.Revisions).ToListAsync(ct);
        return TypedResults.Ok(docs.AsEnumerable());
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Ok<IEnumerable<ShiftAssignment>>> GetShifts(SupportOpsDbContext db, CancellationToken ct) {
        var shifts = await db.ShiftAssignments.OrderBy(x => x.Start).ToListAsync(ct);
        return TypedResults.Ok(shifts.AsEnumerable());
    }

    private static readonly TimeSpan IpWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ActorWindow = TimeSpan.FromHours(1);
    private static readonly TimeSpan BranchWindow = TimeSpan.FromHours(1);
    private const int MaxPerIp = 5;
    private const int MaxPerActor = 10;
    private const int MaxPerBranch = 6;

    private static TimeSpan? EnsureIntakeRateLimit(IMemoryCache cache, HttpContext httpContext, string branch, DateTimeOffset now) {
        TimeSpan? retryAfter = null;
        var clientKey = NormalizeKey(ResolveClientKey(httpContext));
        if (!string.IsNullOrWhiteSpace(clientKey)) {
            retryAfter = MaxRetry(retryAfter, TryConsume(cache, $"support-intake:ip:{clientKey}", MaxPerIp, IpWindow, now));
        }

        var actor = NormalizeKey(ResolveActor(httpContext));
        if (!string.IsNullOrWhiteSpace(actor)) {
            retryAfter = MaxRetry(retryAfter, TryConsume(cache, $"support-intake:actor:{actor}", MaxPerActor, ActorWindow, now));
        }

        var branchKey = NormalizeKey(branch);
        if (!string.IsNullOrWhiteSpace(branchKey)) {
            retryAfter = MaxRetry(retryAfter, TryConsume(cache, $"support-intake:branch:{branchKey}", MaxPerBranch, BranchWindow, now));
        }

        return retryAfter;
    }

    private static TimeSpan? MaxRetry(TimeSpan? current, TimeSpan? next) {
        if (!next.HasValue) {
            return current;
        }

        if (!current.HasValue) {
            return next;
        }

        return current.Value >= next.Value ? current : next;
    }

    private static TimeSpan? TryConsume(IMemoryCache cache, string key, int limit, TimeSpan window, DateTimeOffset now) {
        if (!cache.TryGetValue(key, out RateLimitState? state) || state is null || now - state.WindowStart >= window) {
            state = new RateLimitState(0, now);
        }

        if (state.Count >= limit) {
            var remaining = window - (now - state.WindowStart);
            return remaining <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : remaining;
        }

        var next = state with { Count = state.Count + 1 };
        cache.Set(key, next, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = window });
        return null;
    }

    private static string? ResolveClientKey(HttpContext httpContext) {
        var forwarded = httpContext.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded)) {
            var first = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first)) {
                return first;
            }
        }

        var realIp = httpContext.Request.Headers["X-Real-IP"].ToString();
        if (!string.IsNullOrWhiteSpace(realIp)) {
            return realIp;
        }

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    private static string? ResolveActor(HttpContext httpContext) {
        var user = httpContext.User;
        return user?.Identity?.Name ??
               user?.Claims.FirstOrDefault(c => c.Type is "name" or "preferred_username" or "email")?.Value;
    }

    private static string? NormalizeKey(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private sealed record RateLimitState(int Count, DateTimeOffset WindowStart);
}
