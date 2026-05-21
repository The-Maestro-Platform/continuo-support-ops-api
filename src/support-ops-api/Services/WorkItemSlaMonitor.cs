using Microsoft.EntityFrameworkCore;
using SupportOpsApi.Data;
using SupportOpsApi.Endpoints.Contracts;
using SupportOpsApi.Models;
using Continuo.Coordination;
using Continuo.Shared.Env;

namespace SupportOpsApi.Services;

// 2026-05-19: plain BackgroundService → LeaderOnlyBackgroundService (AGENTS.md §13.17 T2).
// Replica-safe: aynı SLA breach için her replika ayrı bildirim yaratıyordu (DB unique
// constraint olmadığı için duplicate insert + duplicate UI broadcast). Şimdi sadece
// leader SLA evaluate eder, diğer replika dinler.
public sealed class WorkItemSlaMonitor : LeaderOnlyBackgroundService {
    private static readonly TimeSpan Interval = BudgetMode.IsActive
        ? TimeSpan.FromMinutes(10)
        : TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkItemSlaMonitor> _logger;
    private readonly NotificationHub _hub;

    public WorkItemSlaMonitor(
        IDistributedLeaderLock leaderLock,
        IServiceScopeFactory scopeFactory,
        ILogger<WorkItemSlaMonitor> logger,
        NotificationHub hub) : base(leaderLock, logger) {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _hub = hub;
    }

    protected override string LockKey => "support-ops-sla-monitor-tick";
    protected override TimeSpan TickInterval => Interval;

    protected override async Task LeaderTickAsync(CancellationToken ct) {
        try {
            await EvaluateOnce(ct);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "[SLA] Evaluation failed");
        }
    }

    private async Task EvaluateOnce(CancellationToken ct) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
        var policyService = scope.ServiceProvider.GetRequiredService<SlaPolicyService>();

        var policy = await policyService.GetPolicyAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var items = await db.WorkItems
            .Where(x => x.Status != "Done")
            .ToListAsync(ct);

        if (items.Count == 0) {
            return;
        }

        var ids = items.Select(x => x.Id).ToList();
        var existing = await db.WorkItemNotifications
            .Where(x => ids.Contains(x.WorkItemId) && (x.Type == "sla-warning" || x.Type == "sla-breached"))
            .ToListAsync(ct);

        var existingSet = new HashSet<(Guid, string)>(existing.Select(x => (x.WorkItemId, x.Type)));
        var touched = false;

        foreach (var item in items) {
            if (item.SlaTargetAt is null) {
                var minutes = item.SlaMinutes ?? await policyService.ResolveSlaMinutesAsync(item.Type, item.Priority, ct);
                if (minutes > 0) {
                    item.SlaMinutes = minutes;
                    item.SlaTargetAt = now.AddMinutes(minutes);
                    item.UpdatedAt = now;
                    touched = true;
                }
            }
        }

        if (touched) {
            await db.SaveChangesAsync(ct);
        }

        foreach (var item in items) {
            if (item.SlaTargetAt is null) {
                continue;
            }

            var remaining = item.SlaTargetAt.Value - now;
            if (remaining <= TimeSpan.Zero) {
                if (existingSet.Contains((item.Id, "sla-breached"))) {
                    continue;
                }

                await CreateNotificationAsync(db, item, "sla-breached", "critical", "SLA aşıldı", now, ct);
                existingSet.Add((item.Id, "sla-breached"));
                continue;
            }

            if (remaining <= TimeSpan.FromMinutes(policy.WarnBeforeMinutes)) {
                if (existingSet.Contains((item.Id, "sla-warning"))) {
                    continue;
                }

                var message = $"SLA yaklaşmak üzere ({Math.Ceiling(remaining.TotalMinutes)} dk)";
                await CreateNotificationAsync(db, item, "sla-warning", "warning", message, now, ct);
                existingSet.Add((item.Id, "sla-warning"));
            }
        }
    }

    private async Task CreateNotificationAsync(
        TaskFlowDbContext db,
        WorkItem item,
        string type,
        string severity,
        string message,
        DateTimeOffset now,
        CancellationToken ct) {
        var title = $"{item.ExternalId ?? item.Id.ToString()} · {item.Title}";
        var notification = new WorkItemNotification {
            WorkItemId = item.Id,
            Type = type,
            Channel = "in-app",
            Severity = severity,
            Title = title,
            Message = message,
            CreatedAt = now
        };

        db.WorkItemNotifications.Add(notification);
        await db.SaveChangesAsync(ct);

        if (_logger.IsEnabled(LogLevel.Information)) {
            _logger.LogInformation("[SLA] Email/SMS sent for {WorkItem} ({Type})", item.Id, type);
        }
        await _hub.BroadcastAsync(
            new WorkItemNotificationDto(notification.Id, item.Id, type, severity, title, message, notification.Channel, notification.CreatedAt, notification.ReadAt),
            ct
        );
    }
}
