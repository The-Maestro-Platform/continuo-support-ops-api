using Microsoft.EntityFrameworkCore;
using SupportOpsApi.Data;
using SupportOpsApi.Endpoints.Contracts;
using SupportOpsApi.Models;

namespace SupportOpsApi.Services;

/// <summary>
/// Bridges a failed <see cref="SeleniumRun"/> into the existing work-item + notification
/// pipeline. Mirrors the logic in <c>WorkItemHandlers.CreateAutoBug</c> but works against
/// an entity (not an HTTP request) so the runner status-update path can call it in-process.
/// </summary>
public static class SeleniumAutoBugBridge {
    public static async Task<Guid?> CreateAutoBugAsync(
        TaskFlowDbContext db,
        NotificationHub hub,
        IConfiguration configuration,
        SeleniumRun run,
        CancellationToken ct) {
        if (run.Test is null) {
            run.Test = await db.SeleniumTests.FirstOrDefaultAsync(t => t.Id == run.TestId, ct);
        }
        if (run.Test is null) {
            return null;
        }

        // Defense in depth: if the runner mis-classified a skip as a failure (e.g. an older
        // runner build that pre-dates the skip-status support), don't open a bug for it.
        if (LooksLikeSkip(run)) {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var assignee = await ResolveFirstAnalystAsync(db, configuration, ct);
        var tags = new List<string> { "selenium", "e2e", "auto-bug" };
        foreach (var tag in (run.Test.Tags ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            if (!tags.Contains(tag, StringComparer.OrdinalIgnoreCase)) {
                tags.Add(tag);
            }
        }

        var title = TrimTitle($"[Selenium] {run.Test.Name}: {run.FailureMessage ?? "failure"}");
        var bug = new WorkItem {
            Title = title,
            Type = "bug",
            Status = "Backlog",
            Priority = "P2",
            Assignee = assignee,
            Source = "selenium-runner",
            ExternalId = run.Test.Code,
            Tags = string.Join(",", tags),
            CreatedAt = now,
            UpdatedAt = now
        };
        db.WorkItems.Add(bug);

        db.WorkItemStatusChanges.Add(new WorkItemStatusChange {
            WorkItemId = bug.Id,
            FromStatus = null,
            ToStatus = bug.Status,
            Actor = "selenium-runner",
            Note = "auto-bug",
            ChangedAt = now
        });

        var commentText = BuildComment(run);
        if (!string.IsNullOrWhiteSpace(commentText)) {
            db.WorkItemComments.Add(new WorkItemComment {
                WorkItemId = bug.Id,
                Author = "selenium-runner",
                Text = commentText,
                Format = "text",
                CreatedAt = now
            });
        }

        var notification = new WorkItemNotification {
            WorkItemId = bug.Id,
            Type = "auto-bug-created",
            Channel = "in-app",
            Severity = "error",
            Title = $"Auto-bug · {run.Test.Name}",
            Message = $"Selenium run {run.Id} failed and was assigned to {assignee}.",
            CreatedAt = now
        };
        db.WorkItemNotifications.Add(notification);

        await db.SaveChangesAsync(ct);

        try {
            await hub.BroadcastAsync(new WorkItemNotificationDto(
                notification.Id,
                bug.Id,
                notification.Type,
                notification.Severity,
                notification.Title,
                notification.Message,
                notification.Channel,
                notification.CreatedAt,
                notification.ReadAt), ct);
        }
        catch {
            // best-effort — the notification is already persisted
        }

        return bug.Id;
    }

    /// <summary>
    /// When a previously failed test passes again, auto-resolves any open (non-Done) auto-bug
    /// for the same test so the Tasks &amp; Bugs board doesn't keep stale rows. Matches by
    /// <c>Source = "selenium-runner"</c> and <c>ExternalId = test.Code</c>; appends a timestamped
    /// comment explaining the pass and records a status-history entry.
    /// </summary>
    public static async Task<int> AutoResolveAsync(
        TaskFlowDbContext db,
        NotificationHub hub,
        SeleniumRun run,
        CancellationToken ct) {
        if (run.Test is null) {
            run.Test = await db.SeleniumTests.FirstOrDefaultAsync(t => t.Id == run.TestId, ct);
        }
        if (run.Test is null) {
            return 0;
        }

        var code = run.Test.Code;
        var openBugs = await db.WorkItems
            .Where(w => w.Source == "selenium-runner"
                        && w.ExternalId == code
                        && w.Type == "bug"
                        && w.Status != "Done")
            .ToListAsync(ct);
        if (openBugs.Count == 0) {
            return 0;
        }

        var now = DateTimeOffset.UtcNow;
        var localStamp = now.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        var commentText = $"Test {run.Test.Name} ({code}) {localStamp} tarihinde tekrar başarıyla geçti. " +
                          $"Run: {run.Id}. Bug otomatik olarak Done statüsüne çekildi.";

        foreach (var bug in openBugs) {
            var previous = bug.Status;
            bug.Status = "Done";
            bug.UpdatedAt = now;
            if (string.IsNullOrWhiteSpace(bug.ResolutionNotes)) {
                bug.ResolutionNotes = $"Auto-resolved by selenium-runner on {localStamp} (run {run.Id}).";
            }

            db.WorkItemStatusChanges.Add(new WorkItemStatusChange {
                WorkItemId = bug.Id,
                FromStatus = previous,
                ToStatus = bug.Status,
                Actor = "selenium-runner",
                Note = "auto-resolved (test passed)",
                ChangedAt = now
            });

            db.WorkItemComments.Add(new WorkItemComment {
                WorkItemId = bug.Id,
                Author = "selenium-runner",
                Text = commentText,
                Format = "text",
                CreatedAt = now
            });

            db.WorkItemNotifications.Add(new WorkItemNotification {
                WorkItemId = bug.Id,
                Type = "auto-bug-resolved",
                Channel = "in-app",
                Severity = "info",
                Title = $"Auto-bug resolved · {run.Test.Name}",
                Message = $"Selenium test {code} passed — bug moved to Done.",
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);

        try {
            await hub.BroadcastAsync(new WorkItemNotificationDto(
                Guid.NewGuid(),
                openBugs[0].Id,
                "auto-bug-resolved",
                "info",
                $"Auto-bug resolved · {run.Test.Name}",
                $"{openBugs.Count} bug kaydı otomatik kapatıldı.",
                "in-app",
                now,
                null), ct);
        }
        catch {
            // best-effort — DB state is already persisted
        }

        return openBugs.Count;
    }

    private static string BuildComment(SeleniumRun run) {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Test: {run.Test?.Name} ({run.Test?.Code})");
        sb.AppendLine($"Run: {run.Id}");
        if (run.Runner is not null) {
            sb.AppendLine($"Runner: {run.Runner.Name} ({run.Runner.Location})");
        }
        if (run.DurationMs > 0) {
            sb.AppendLine($"Duration: {run.DurationMs} ms");
        }
        if (!string.IsNullOrWhiteSpace(run.FailureMessage)) {
            sb.AppendLine();
            sb.AppendLine("Failure:");
            sb.AppendLine(run.FailureMessage);
        }
        if (!string.IsNullOrWhiteSpace(run.Stderr)) {
            sb.AppendLine();
            sb.AppendLine("Stderr (tail):");
            sb.AppendLine(TailOf(run.Stderr, 40));
        } else if (!string.IsNullOrWhiteSpace(run.Stdout)) {
            sb.AppendLine();
            sb.AppendLine("Stdout (tail):");
            sb.AppendLine(TailOf(run.Stdout, 40));
        }
        if (!string.IsNullOrWhiteSpace(run.ScreenshotRef)) {
            sb.AppendLine();
            sb.AppendLine($"Screenshot: {run.ScreenshotRef}");
        }
        return sb.ToString().Trim();
    }

    private static bool LooksLikeSkip(SeleniumRun run) {
        const string marker = "$XunitDynamicSkip$";
        if (!string.IsNullOrWhiteSpace(run.FailureMessage) && run.FailureMessage.Contains(marker, StringComparison.Ordinal)) {
            return true;
        }
        if (!string.IsNullOrWhiteSpace(run.Stdout) && run.Stdout.Contains(marker, StringComparison.Ordinal)) {
            return true;
        }
        return false;
    }

    private static string TailOf(string value, int lines) {
        var split = value.Split('\n');
        if (split.Length <= lines) {
            return value;
        }
        return string.Join('\n', split[^lines..]);
    }

    private static string TrimTitle(string title) {
        if (string.IsNullOrWhiteSpace(title)) {
            return "Selenium failure";
        }
        var firstLine = title.Split('\n').FirstOrDefault()?.Trim() ?? title;
        return firstLine.Length > 230 ? string.Concat(firstLine.AsSpan(0, 227), "...") : firstLine;
    }

    private static async Task<string> ResolveFirstAnalystAsync(TaskFlowDbContext db, IConfiguration configuration, CancellationToken ct) {
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
        var anyAssignee = await db.WorkItems
            .AsNoTracking()
            .Where(w => w.Assignee != null && w.Assignee != string.Empty)
            .OrderByDescending(w => w.UpdatedAt)
            .Select(w => w.Assignee)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(anyAssignee)) {
            return anyAssignee!;
        }
        var configured = configuration["SupportOps:AutoBugDefaultAssignee"]
                         ?? Environment.GetEnvironmentVariable("SUPPORT_OPS_AUTO_BUG_ASSIGNEE");
        return string.IsNullOrWhiteSpace(configured) ? "analyst@example.local" : configured.Trim();
    }
}
