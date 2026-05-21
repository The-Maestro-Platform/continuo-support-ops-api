using System.Text;
using System.Text.Json;
using Ganss.Xss;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SupportOpsApi.Data;
using SupportOpsApi.Endpoints.Contracts;
using SupportOpsApi.Models;
using SupportOpsApi.Services;
using Continuo.Observability.Attributes;

namespace SupportOpsApi.Endpoints.Handlers;

public static class WorkItemHandlers {
    [ContinuoProxyMethod("ui")]
    public static async Task<Ok<WorkItemDto[]>> GetWorkItems(TaskFlowDbContext db, DmsApiClient dmsClient, string? status, string? type, string? assignee, CancellationToken ct) {
        var query = db.WorkItems
            .Include(x => x.Links).ThenInclude(x => x.RelatedWorkItem)
            .Include(x => x.DocumentLinks)
            .Include(x => x.Comments)
            .Include(x => x.Attachments).ThenInclude(x => x.File)
            .Include(x => x.StatusHistory)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) {
            query = query.Where(x => x.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(type)) {
            query = query.Where(x => x.Type == type);
        }
        if (!string.IsNullOrWhiteSpace(assignee)) {
            query = query.Where(x => x.Assignee == assignee);
        }

        var items = await query
            .OrderByDescending(x => x.UpdatedAt)
            .Take(200)
            .ToListAsync(ct);

        var dtos = await Task.WhenAll(items.Select(item => ToDtoAsync(item, dmsClient, ct)));
        return TypedResults.Ok(dtos);
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<WorkItemDto>, NotFound>> GetWorkItemById(Guid id, TaskFlowDbContext db, DmsApiClient dmsClient, CancellationToken ct) {
        var item = await db.WorkItems
            .Include(x => x.Links).ThenInclude(x => x.RelatedWorkItem)
            .Include(x => x.DocumentLinks)
            .Include(x => x.Comments)
            .Include(x => x.Attachments).ThenInclude(x => x.File)
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? TypedResults.NotFound() : TypedResults.Ok(await ToDtoAsync(item, dmsClient, ct));
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Created<WorkItemDto>> CreateWorkItem(TaskFlowDbContext db, UpsertWorkItemRequest request, SlaPolicyService slaPolicy, DmsApiClient dmsClient, HttpContext httpContext, CancellationToken ct) {
        var actor = ResolveActor(httpContext);
        var now = DateTimeOffset.UtcNow;
        var priority = string.IsNullOrWhiteSpace(request.Priority) ? "P3" : request.Priority;
        var slaMinutes = request.SlaMinutes;
        var slaTargetAt = request.SlaTargetAt;
        if (slaMinutes is null || slaMinutes <= 0) {
            slaMinutes = await slaPolicy.ResolveSlaMinutesAsync(request.Type, priority, ct);
        }
        if (slaTargetAt is null && slaMinutes is > 0) {
            slaTargetAt = now.AddMinutes(slaMinutes.Value);
        }
        if (slaTargetAt is not null && (slaMinutes is null || slaMinutes <= 0)) {
            slaMinutes = (int)Math.Round((slaTargetAt.Value - now).TotalMinutes);
        }

        var entity = new WorkItem {
            Title = request.Title,
            Type = request.Type,
            Status = request.Status ?? "Backlog",
            Priority = priority,
            Assignee = request.Assignee ?? string.Empty,
            Source = request.Source ?? string.Empty,
            ExternalId = request.ExternalId ?? string.Empty,
            BugServiceId = request.BugServiceId,
            BugServiceName = request.BugServiceName,
            BugEndpointId = request.BugEndpointId,
            BugEndpointPath = request.BugEndpointPath,
            BugEndpointMethod = request.BugEndpointMethod,
            ResolutionNotes = request.ResolutionNotes,
            GithubRepo = request.Github?.Repo,
            GithubBranch = request.Github?.Branch,
            GithubCommit = request.Github?.Commit,
            GithubPullRequest = request.Github?.PullRequest,
            Tags = string.Join(",", request.Tags ?? Array.Empty<string>()),
            SlaMinutes = slaMinutes,
            SlaTargetAt = slaTargetAt,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.WorkItems.Add(entity);
        var createdHistory = new WorkItemStatusChange {
            WorkItemId = entity.Id,
            FromStatus = null,
            ToStatus = entity.Status,
            Actor = actor,
            Note = "created",
            ChangedAt = entity.CreatedAt
        };
        entity.StatusHistory.Add(createdHistory);
        db.WorkItemStatusChanges.Add(createdHistory);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/work-items/{entity.Id}", await ToDtoAsync(entity, dmsClient, ct));
    }

    /// <summary>
    /// Auto-bug endpoint used by automated test runners (Selenium E2E, health checks).
    /// Creates a bug work item, attaches a comment with the failure context, picks the first
    /// available analyst if none provided, and fires an in-app notification via the hub.
    /// </summary>
    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Created<AutoBugResponse>, BadRequest<string>>> CreateAutoBug(
        AutoBugRequest request,
        TaskFlowDbContext db,
        NotificationHub hub,
        IConfiguration configuration,
        HttpContext httpContext,
        CancellationToken ct) {
        if (request is null || string.IsNullOrWhiteSpace(request.Title)) {
            return TypedResults.BadRequest("Title is required");
        }

        var now = DateTimeOffset.UtcNow;
        var actor = ResolveActor(httpContext);

        var tagList = (request.Tags ?? Array.Empty<string>())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .ToList();
        foreach (var defaultTag in new[] { "selenium", "e2e", "auto-bug" }) {
            if (!tagList.Any(t => string.Equals(t, defaultTag, StringComparison.OrdinalIgnoreCase))) {
                tagList.Add(defaultTag);
            }
        }

        // Failure signature lets us collapse repeated failures of the same test/exception
        // into a single bug. Format: sig:<10-char hex of testName + first line of summary>.
        // We tag the bug with this signature so future runs can find it via a simple
        // Tags.Contains() query without needing a new column / migration.
        var signatureTag = ComputeAutoBugSignatureTag(request);
        if (!tagList.Any(t => string.Equals(t, signatureTag, StringComparison.OrdinalIgnoreCase))) {
            tagList.Add(signatureTag);
        }

        var resolvedSource = string.IsNullOrWhiteSpace(request.Source) ? "selenium-e2e" : request.Source!;
        var externalId = request.TestName ?? string.Empty;

        // Look for an existing OPEN bug with the same signature in the last 14 days. If we
        // find one, we attach a recurrence comment instead of opening a brand new task —
        // this stops Selenium runs from spamming the board with duplicate cards every time
        // an environment regression is in place.
        var duplicateLookbackStart = now.AddDays(-14);
        var existingDuplicate = await db.WorkItems
            .Where(w => w.Type == "bug")
            .Where(w => w.Source == resolvedSource)
            .Where(w => w.ExternalId == externalId)
            .Where(w => w.UpdatedAt >= duplicateLookbackStart)
            .Where(w => !ClosedAutoBugStatuses.Contains(w.Status))
            .Where(w => w.Tags.Contains(signatureTag))
            .OrderByDescending(w => w.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        if (existingDuplicate is not null) {
            existingDuplicate.UpdatedAt = now;
            var recurrenceComment = BuildAutoBugRecurrenceComment(request, now);
            if (!string.IsNullOrWhiteSpace(recurrenceComment)) {
                db.WorkItemComments.Add(new WorkItemComment {
                    WorkItemId = existingDuplicate.Id,
                    Author = actor,
                    Text = recurrenceComment!,
                    Format = "text",
                    CreatedAt = now
                });
            }

            // Re-fire an in-app notification so the assignee still sees the recurrence,
            // but tag it as 'recurrence' so the UI can render it differently if desired.
            var dupNotification = new WorkItemNotification {
                WorkItemId = existingDuplicate.Id,
                Type = "auto-bug-recurrence",
                Channel = "in-app",
                Severity = "warning",
                Title = $"Auto-bug recurrence · {existingDuplicate.Title}",
                Message = string.IsNullOrWhiteSpace(request.TestName)
                    ? $"Selenium test failed again with the same signature. Assigned to {existingDuplicate.Assignee}."
                    : $"Selenium test '{request.TestName}' failed again. Assigned to {existingDuplicate.Assignee}.",
                CreatedAt = now
            };
            db.WorkItemNotifications.Add(dupNotification);

            await db.SaveChangesAsync(ct);

            try {
                await hub.BroadcastAsync(
                    new WorkItemNotificationDto(
                        dupNotification.Id,
                        existingDuplicate.Id,
                        dupNotification.Type,
                        dupNotification.Severity,
                        dupNotification.Title,
                        dupNotification.Message,
                        dupNotification.Channel,
                        dupNotification.CreatedAt,
                        dupNotification.ReadAt),
                    ct);
            }
            catch {
                // best-effort broadcast — comment + notification are already persisted.
            }

            return TypedResults.Created(
                $"/work-items/{existingDuplicate.Id}",
                new AutoBugResponse(existingDuplicate.Id, dupNotification.Id, existingDuplicate.Assignee));
        }

        var assignee = await ResolveFirstAnalystAsync(request.Assignee, db, configuration, ct);

        var entity = new WorkItem {
            Title = request.Title.Trim(),
            Type = "bug",
            Status = "Backlog",
            Priority = string.IsNullOrWhiteSpace(request.Priority) ? "P2" : request.Priority!,
            Assignee = assignee,
            Source = resolvedSource,
            ExternalId = externalId,
            Tags = string.Join(",", tagList),
            CreatedAt = now,
            UpdatedAt = now
        };

        db.WorkItems.Add(entity);

        var history = new WorkItemStatusChange {
            WorkItemId = entity.Id,
            FromStatus = null,
            ToStatus = entity.Status,
            Actor = actor,
            Note = "auto-bug",
            ChangedAt = now
        };
        entity.StatusHistory.Add(history);
        db.WorkItemStatusChanges.Add(history);

        var commentText = BuildAutoBugComment(request);
        if (!string.IsNullOrWhiteSpace(commentText)) {
            db.WorkItemComments.Add(new WorkItemComment {
                WorkItemId = entity.Id,
                Author = actor,
                Text = commentText!,
                Format = "text",
                CreatedAt = now
            });
        }

        var notificationTitle = $"Auto-bug · {entity.Title}";
        var notificationMessage = string.IsNullOrWhiteSpace(request.TestName)
            ? $"Selenium test raised a bug assigned to {assignee}."
            : $"Selenium test '{request.TestName}' raised a bug assigned to {assignee}.";

        var notification = new WorkItemNotification {
            WorkItemId = entity.Id,
            Type = "auto-bug-created",
            Channel = "in-app",
            Severity = "error",
            Title = notificationTitle,
            Message = notificationMessage,
            CreatedAt = now
        };
        db.WorkItemNotifications.Add(notification);

        await db.SaveChangesAsync(ct);

        try {
            await hub.BroadcastAsync(
                new WorkItemNotificationDto(
                    notification.Id,
                    entity.Id,
                    notification.Type,
                    notification.Severity,
                    notification.Title,
                    notification.Message,
                    notification.Channel,
                    notification.CreatedAt,
                    notification.ReadAt),
                ct);
        }
        catch {
            // broadcast best-effort: the notification is already persisted so the analyst
            // will still see it on the next poll / reconnect.
        }

        return TypedResults.Created(
            $"/work-items/{entity.Id}",
            new AutoBugResponse(entity.Id, notification.Id, assignee));
    }

    /// <summary>
    /// Statuses that count an auto-bug as "still open" for duplicate detection. Anything
    /// outside this set (Done / Closed / Resolved / Cancelled) means the previous bug was
    /// addressed and a new failure should open a fresh task instead of reopening it.
    /// </summary>
    private static readonly string[] ClosedAutoBugStatuses = new[] { "Done", "Closed", "Resolved", "Cancelled" };

    /// <summary>
    /// Builds the deterministic signature tag used to detect duplicate auto-bugs. The tag
    /// is the SHA-256 prefix of the test name + first non-empty line of the failure summary
    /// so the same exception in the same test always collapses onto the same bug.
    /// </summary>
    private static string ComputeAutoBugSignatureTag(AutoBugRequest request) {
        var testName = (request.TestName ?? string.Empty).Trim();
        var firstLine = (request.Summary ?? request.Title ?? string.Empty)
            .Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.Length > 0) ?? string.Empty;
        var raw = $"{testName}|{firstLine}".ToLowerInvariant();
        var bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        var hex = Convert.ToHexString(bytes, 0, 5).ToLowerInvariant();
        return $"sig:{hex}";
    }

    private static string? BuildAutoBugRecurrenceComment(AutoBugRequest request, DateTimeOffset now) {
        var builder = new StringBuilder();
        builder.AppendLine($"Recurrence at {now:O}");
        if (!string.IsNullOrWhiteSpace(request.Summary)) {
            builder.AppendLine();
            builder.AppendLine(request.Summary);
        }
        if (!string.IsNullOrWhiteSpace(request.ScreenshotRef)) {
            builder.AppendLine();
            builder.AppendLine($"Screenshot: {request.ScreenshotRef}");
        }
        if (!string.IsNullOrWhiteSpace(request.LogTail)) {
            builder.AppendLine();
            builder.AppendLine("Log tail:");
            builder.AppendLine(request.LogTail);
        }
        var text = builder.ToString().Trim();
        return text.Length == 0 ? null : text;
    }

    private static string? BuildAutoBugComment(AutoBugRequest request) {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(request.TestName)) {
            builder.AppendLine($"Test: {request.TestName}");
        }
        if (!string.IsNullOrWhiteSpace(request.Source)) {
            builder.AppendLine($"Source: {request.Source}");
        }
        if (!string.IsNullOrWhiteSpace(request.Summary)) {
            builder.AppendLine();
            builder.AppendLine(request.Summary);
        }
        if (!string.IsNullOrWhiteSpace(request.StackTrace)) {
            builder.AppendLine();
            builder.AppendLine("Stack trace:");
            builder.AppendLine(request.StackTrace);
        }
        if (!string.IsNullOrWhiteSpace(request.LogTail)) {
            builder.AppendLine();
            builder.AppendLine("Log tail:");
            builder.AppendLine(request.LogTail);
        }
        if (!string.IsNullOrWhiteSpace(request.ScreenshotRef)) {
            builder.AppendLine();
            builder.AppendLine($"Screenshot: {request.ScreenshotRef}");
        }
        var text = builder.ToString().Trim();
        return text.Length == 0 ? null : text;
    }

    private static async Task<string> ResolveFirstAnalystAsync(
        string? requested,
        TaskFlowDbContext db,
        IConfiguration configuration,
        CancellationToken ct) {
        if (!string.IsNullOrWhiteSpace(requested)) {
            return requested!.Trim();
        }

        // 1. Most recent active work item assignee whose tags mention "analyst" or "support".
        //    This gives us a live "first analyst we know about" without needing a cross-service
        //    call that would require M2M credentials in the auto-bug path.
        var recentAnalyst = await db.WorkItems
            .AsNoTracking()
            .Where(w => w.Assignee != null && w.Assignee != string.Empty)
            .Where(w => w.Tags.Contains("analyst") || w.Tags.Contains("support") || w.Tags.Contains("devsup"))
            .OrderByDescending(w => w.UpdatedAt)
            .Select(w => w.Assignee)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(recentAnalyst)) {
            return recentAnalyst!;
        }

        // 2. Any most-recently touched work item assignee — avoids an empty Assignee on brand
        //    new environments that still want to receive the notification.
        var anyAssignee = await db.WorkItems
            .AsNoTracking()
            .Where(w => w.Assignee != null && w.Assignee != string.Empty)
            .OrderByDescending(w => w.UpdatedAt)
            .Select(w => w.Assignee)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(anyAssignee)) {
            return anyAssignee!;
        }

        // 3. Config-driven fallback: SupportOps:AutoBugDefaultAssignee or env var.
        var configured = configuration["SupportOps:AutoBugDefaultAssignee"]
                         ?? Environment.GetEnvironmentVariable("SUPPORT_OPS_AUTO_BUG_ASSIGNEE");
        if (!string.IsNullOrWhiteSpace(configured)) {
            return configured!.Trim();
        }

        return "analyst@example.local";
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<WorkItemDto>, NotFound>> UpdateWorkItem(Guid id, UpsertWorkItemRequest request, TaskFlowDbContext db, DmsApiClient dmsClient, HttpContext httpContext, CancellationToken ct) {
        var entity = await db.WorkItems
            .Include(x => x.Links).ThenInclude(x => x.RelatedWorkItem)
            .Include(x => x.Attachments).ThenInclude(x => x.File)
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) {
            return TypedResults.NotFound();
        }

        var previousStatus = entity.Status;
        entity.Title = request.Title;
        entity.Type = request.Type;
        entity.Status = request.Status ?? entity.Status;
        entity.Priority = request.Priority ?? entity.Priority;
        entity.Assignee = request.Assignee ?? entity.Assignee;
        entity.Source = request.Source ?? entity.Source;
        entity.ExternalId = request.ExternalId ?? entity.ExternalId;
        entity.BugServiceId = request.BugServiceId;
        entity.BugServiceName = request.BugServiceName;
        entity.BugEndpointId = request.BugEndpointId;
        entity.BugEndpointPath = request.BugEndpointPath;
        entity.BugEndpointMethod = request.BugEndpointMethod;
        entity.ResolutionNotes = request.ResolutionNotes;
        if (request.SlaMinutes.HasValue && request.SlaMinutes.Value > 0) {
            entity.SlaMinutes = request.SlaMinutes.Value;
            entity.SlaTargetAt = request.SlaTargetAt ?? DateTimeOffset.UtcNow.AddMinutes(request.SlaMinutes.Value);
        }
        else if (request.SlaTargetAt.HasValue) {
            entity.SlaTargetAt = request.SlaTargetAt;
            entity.SlaMinutes = (int)Math.Round((request.SlaTargetAt.Value - DateTimeOffset.UtcNow).TotalMinutes);
        }
        entity.GithubRepo = request.Github?.Repo;
        entity.GithubBranch = request.Github?.Branch;
        entity.GithubCommit = request.Github?.Commit;
        entity.GithubPullRequest = request.Github?.PullRequest;
        entity.Tags = string.Join(",", request.Tags ?? Array.Empty<string>());
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        if (!string.Equals(previousStatus, entity.Status, StringComparison.OrdinalIgnoreCase)) {
            var history = new WorkItemStatusChange {
                WorkItemId = entity.Id,
                FromStatus = previousStatus,
                ToStatus = entity.Status,
                Actor = ResolveActor(httpContext),
                Note = null,
                ChangedAt = entity.UpdatedAt
            };
            entity.StatusHistory.Add(history);
            db.WorkItemStatusChanges.Add(history);
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ToDtoAsync(entity, dmsClient, ct));
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<NoContent, NotFound>> DeleteWorkItem(Guid id, TaskFlowDbContext db, CancellationToken ct) {
        var entity = await db.WorkItems.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) {
            return TypedResults.NotFound();
        }

        db.WorkItems.Remove(entity);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<WorkItemDto>, NotFound, BadRequest<string>>> CreateWorkItemLink(Guid id, CreateLinkRequest request, TaskFlowDbContext db, DmsApiClient dmsClient, CancellationToken ct) {
        var entity = await db.WorkItems.Include(x => x.Links).ThenInclude(x => x.RelatedWorkItem).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) {
            return TypedResults.NotFound();
        }

        var targetExists = await db.WorkItems.AnyAsync(x => x.Id == request.RelatedWorkItemId, ct);
        if (!targetExists) {
            return TypedResults.BadRequest("Related work item not found");
        }

        var link = new WorkItemLink {
            WorkItemId = id,
            RelatedWorkItemId = request.RelatedWorkItemId,
            Relation = string.IsNullOrWhiteSpace(request.Relation) ? "relates to" : request.Relation
        };
        db.WorkItemLinks.Add(link);
        await db.SaveChangesAsync(ct);
        await db.Entry(entity).Collection(x => x.Links).Query().Include(x => x.RelatedWorkItem).LoadAsync(ct);
        return TypedResults.Ok(await ToDtoAsync(entity, dmsClient, ct));
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<NoContent, NotFound>> DeleteWorkItemLink(Guid id, Guid linkId, TaskFlowDbContext db, CancellationToken ct) {
        var link = await db.WorkItemLinks.FirstOrDefaultAsync(x => x.Id == linkId && x.WorkItemId == id, ct);
        if (link is null) {
            return TypedResults.NotFound();
        }

        db.WorkItemLinks.Remove(link);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    [ContinuoProxyMethod("ui")]
    [ContinuoProxyUiHtml]
    public static async Task<Results<Created<WorkItemCommentDto>, NotFound, BadRequest<string>>> AddWorkItemComment(Guid id, CreateWorkItemCommentRequest request, TaskFlowDbContext db, HttpContext httpContext, CancellationToken ct) {
        var workItemExists = await db.WorkItems.AnyAsync(x => x.Id == id, ct);
        if (!workItemExists) {
            return TypedResults.NotFound();
        }

        var format = string.Equals(request.Format, "html", StringComparison.OrdinalIgnoreCase) ? "html" : "text";
        var text = request.Text ?? string.Empty;
        if (format == "html") {
            text = SanitizeHtml(text);
        }

        var author = ResolveActor(httpContext);
        var entity = new WorkItemComment {
            WorkItemId = id,
            Author = author,
            Text = text,
            Format = format,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.WorkItemComments.Add(entity);
        await db.SaveChangesAsync(ct);

        var dto = new WorkItemCommentDto(entity.Id, entity.Author, entity.Text, entity.Format, entity.CreatedAt);
        return TypedResults.Created($"/work-items/{id}/comments/{entity.Id}", dto);
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Created<WorkItemAttachmentDto>, NotFound, BadRequest<string>>> UploadWorkItemAttachment(Guid id, IFormFile file, string? kind, TaskFlowDbContext db, DmsApiClient dmsClient, HttpContext httpContext, CancellationToken ct) {
        if (file is null) {
            return TypedResults.BadRequest("File is required");
        }

        var exists = await db.WorkItems.AnyAsync(x => x.Id == id, ct);
        if (!exists) {
            return TypedResults.NotFound();
        }

        kind = string.Equals(kind, "screenshot", StringComparison.OrdinalIgnoreCase) ? "screenshot" : "attachment";

        var dmsItem = await dmsClient.CreateItemAsync(
            new CreateDmsItemRequest(
                file,
                file.FileName,
                "support-ops-api",
                Description: null,
                Note: null,
                Tags: new[]
                {
                    new DmsTagDto("workItemId", id.ToString()),
                    new DmsTagDto("kind", kind)
                }),
            ct);

        var attachment = new WorkItemAttachment {
            WorkItemId = id,
            DmsItemId = dmsItem.Id,
            Kind = kind,
            UploadedBy = ResolveActor(httpContext),
            UploadedAt = DateTimeOffset.UtcNow
        };
        db.WorkItemAttachments.Add(attachment);

        await db.SaveChangesAsync(ct);

        var dto = ToAttachmentDto(attachment, dmsItem);
        return TypedResults.Created($"/work-items/{id}/attachments/{attachment.Id}", dto);
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<FileStreamHttpResult, NotFound>> DownloadFile(Guid id, TaskFlowDbContext db, DmsStorage storage, HttpContext httpContext, CancellationToken ct) {
        var entity = await db.DmsFiles.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) {
            return TypedResults.NotFound();
        }

        var absolute = storage.ResolveAbsolutePath(entity.StoragePath);
        if (!File.Exists(absolute)) {
            return TypedResults.NotFound();
        }

        httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";

        var inline = entity.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        var stream = File.OpenRead(absolute);
        return TypedResults.File(stream, entity.ContentType, fileDownloadName: inline ? null : entity.FileName, enableRangeProcessing: true);
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Ok<IEnumerable<DocumentDto>>> GetDocuments(TaskFlowDbContext db, CancellationToken ct) {
        var docs = await db.Documents.Include(x => x.WorkItemLinks).OrderByDescending(x => x.LastUpdated).Take(200).ToListAsync(ct);
        return TypedResults.Ok(docs.Select(ToDto));
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<DocumentDto>, NotFound>> GetDocumentById(Guid id, TaskFlowDbContext db, CancellationToken ct) {
        var doc = await db.Documents.Include(x => x.WorkItemLinks).FirstOrDefaultAsync(x => x.Id == id, ct);
        return doc is null ? TypedResults.NotFound() : TypedResults.Ok(ToDto(doc));
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Created<DocumentDto>> CreateDocument(TaskFlowDbContext db, UpsertDocumentRequest request, CancellationToken ct) {
        var entity = new DocumentRecord {
            Title = request.Title,
            Category = request.Category ?? "Analysis",
            Status = request.Status ?? "Draft",
            Owner = request.Owner ?? string.Empty,
            Tags = string.Join(",", request.Tags ?? Array.Empty<string>()),
            Link = request.Link,
            LastUpdated = DateTimeOffset.UtcNow
        };

        db.Documents.Add(entity);
        if (request.RelatedWorkItemIds is not null && request.RelatedWorkItemIds.Any()) {
            foreach (var workItemId in request.RelatedWorkItemIds) {
                db.DocumentLinks.Add(new DocumentLink { DocumentRecordId = entity.Id, WorkItemId = workItemId });
            }
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/documents/{entity.Id}", ToDto(entity));
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<DocumentDto>, NotFound>> UpdateDocument(Guid id, UpsertDocumentRequest request, TaskFlowDbContext db, CancellationToken ct) {
        var entity = await db.Documents.Include(x => x.WorkItemLinks).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) {
            return TypedResults.NotFound();
        }

        entity.Title = request.Title;
        entity.Category = request.Category ?? entity.Category;
        entity.Status = request.Status ?? entity.Status;
        entity.Owner = request.Owner ?? entity.Owner;
        entity.Tags = string.Join(",", request.Tags ?? Array.Empty<string>());
        entity.Link = request.Link ?? entity.Link;
        entity.LastUpdated = DateTimeOffset.UtcNow;

        if (request.RelatedWorkItemIds is not null) {
            var existingLinks = await db.DocumentLinks.Where(x => x.DocumentRecordId == id).ToListAsync(ct);
            db.DocumentLinks.RemoveRange(existingLinks);
            foreach (var workItemId in request.RelatedWorkItemIds) {
                db.DocumentLinks.Add(new DocumentLink { DocumentRecordId = id, WorkItemId = workItemId });
            }
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(entity));
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<NoContent, NotFound>> DeleteDocument(Guid id, TaskFlowDbContext db, CancellationToken ct) {
        var entity = await db.Documents.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) {
            return TypedResults.NotFound();
        }

        db.Documents.Remove(entity);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<WorkItemDto> ToDtoAsync(WorkItem entity, DmsApiClient dmsClient, CancellationToken ct) {
        var attachments = entity.Attachments
            .OrderByDescending(x => x.UploadedAt)
            .ToList();
        var attachmentDtos = await Task.WhenAll(attachments.Select(att => ToAttachmentDtoAsync(att, dmsClient, ct)));

        return new WorkItemDto(
            entity.Id,
            string.IsNullOrWhiteSpace(entity.ExternalId) ? null : entity.ExternalId,
            entity.Title,
            entity.Type,
            entity.Status,
            entity.Priority,
            entity.Assignee,
            entity.Source,
            entity.BugServiceId,
            entity.BugServiceName,
            entity.BugEndpointId,
            entity.BugEndpointPath,
            entity.BugEndpointMethod,
            entity.ResolutionNotes,
            entity.SlaMinutes,
            entity.SlaTargetAt,
            entity.GithubRepo,
            entity.GithubBranch,
            entity.GithubCommit,
            entity.GithubPullRequest,
            (entity.Tags ?? string.Empty).Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            entity.Links.Select(l => new WorkItemRelation(l.RelatedWorkItemId, l.Relation, l.RelatedWorkItem?.Type ?? "task", l.RelatedWorkItem?.Title ?? string.Empty, l.Id)).ToList(),
            entity.Comments.OrderByDescending(x => x.CreatedAt).Select(c => new WorkItemCommentDto(c.Id, c.Author, c.Text, c.Format, c.CreatedAt)).ToList(),
            attachmentDtos.ToList(),
            entity.StatusHistory.OrderByDescending(x => x.ChangedAt).Select(x => new WorkItemStatusChangeDto(x.Id, x.FromStatus, x.ToStatus, x.Actor, x.ChangedAt, x.Note)).ToList()
        );
    }

    private static async Task<WorkItemAttachmentDto> ToAttachmentDtoAsync(WorkItemAttachment entity, DmsApiClient dmsClient, CancellationToken ct) {
        if (entity.DmsItemId.HasValue) {
            var dmsItem = await dmsClient.GetItemAsync(entity.DmsItemId.Value, ct);
            if (dmsItem is not null) {
                return ToAttachmentDto(entity, dmsItem);
            }
        }

        var file = entity.File;
        if (file is not null && entity.FileId.HasValue) {
            return new WorkItemAttachmentDto(
                entity.Id,
                entity.FileId.Value,
                file.FileName,
                file.ContentType,
                file.Length,
                entity.Kind,
                $"/work-items/files/{entity.FileId}",
                entity.UploadedAt,
                entity.UploadedBy
            );
        }

        return new WorkItemAttachmentDto(
            entity.Id,
            Guid.Empty,
            entity.DmsItemId?.ToString() ?? entity.Id.ToString(),
            "application/octet-stream",
            0,
            entity.Kind,
            string.Empty,
            entity.UploadedAt,
            entity.UploadedBy
        );
    }

    private static WorkItemAttachmentDto ToAttachmentDto(WorkItemAttachment entity, DmsItemDto dmsItem) {
        var version = ResolveCurrentVersion(dmsItem);
        if (version is null) {
            return new WorkItemAttachmentDto(
                entity.Id,
                Guid.Empty,
                dmsItem.Title,
                "application/octet-stream",
                0,
                entity.Kind,
                string.Empty,
                entity.UploadedAt,
                entity.UploadedBy
            );
        }

        return new WorkItemAttachmentDto(
            entity.Id,
            version.FileId,
            version.FileName,
            version.ContentType,
            version.Length,
            entity.Kind,
            $"/dms/files/{version.FileId}",
            entity.UploadedAt,
            entity.UploadedBy
        );
    }

    private static DmsItemVersionDto? ResolveCurrentVersion(DmsItemDto item) {
        if (item.Versions is null || item.Versions.Count == 0) {
            return null;
        }

        var byId = item.Versions.FirstOrDefault(v => v.Id == item.CurrentVersionId);
        if (byId is not null) {
            return byId;
        }

        return item.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
    }

    private static DocumentDto ToDto(DocumentRecord doc) {
        return new DocumentDto(
            doc.Id,
            doc.Title,
            doc.Category,
            doc.Status,
            doc.Owner,
            doc.LastUpdated,
            (doc.Tags ?? string.Empty).Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            doc.WorkItemLinks.Select(x => x.WorkItemId).ToList(),
            doc.Link
        );
    }

    private static string ResolveActor(HttpContext httpContext) {
        var user = httpContext.User;
        var candidate =
            user?.Identity?.Name ??
            user?.Claims.FirstOrDefault(c => c.Type is "name" or "preferred_username" or "email")?.Value;
        if (!string.IsNullOrWhiteSpace(candidate)) {
            return candidate;
        }

        var fromBearer = TryResolveActorFromBearer(httpContext);
        return string.IsNullOrWhiteSpace(fromBearer) ? "System" : fromBearer!;
    }

    private static string? TryResolveActorFromBearer(HttpContext httpContext) {
        var auth = httpContext.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        var token = auth["Bearer ".Length..].Trim();
        var parts = token.Split('.');
        if (parts.Length < 2) {
            return null;
        }

        try {
            var payload = parts[1];
            payload = payload.Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            foreach (var key in new[] { "preferred_username", "name", "email", "upn", "sub" }) {
                if (root.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String) {
                    var value = prop.GetString();
                    if (!string.IsNullOrWhiteSpace(value)) {
                        return value;
                    }
                }
            }
        }
        catch {
            return null;
        }

        return null;
    }

    private static string SanitizeHtml(string html) {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedAttributes.Clear();

        foreach (var tag in new[] { "p", "br", "b", "strong", "i", "em", "ul", "ol", "li", "pre", "code", "blockquote", "a" }) {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Add("href");
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");

        var cleaned = sanitizer.Sanitize(html ?? string.Empty);
        return cleaned;
    }
}
