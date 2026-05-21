using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupportOpsApi.Data;
using SupportOpsApi.Endpoints.Contracts;
using SupportOpsApi.Models;
using Continuo.Shared.Contracts;

namespace SupportOpsApi.Services;

/// <summary>
/// Detects upstream service failures (5xx, gateway timeouts, connection refused,
/// failed websocket handshakes) coming through SystemHttpLogConsumer and turns
/// them into a high-SLA WorkItem + WorkItemNotification, broadcast live to the
/// dev-support console via NotificationHub. Idempotent within a 5-minute bucket
/// per (target service, status bucket) so a flapping service does not spam the
/// inbox.
/// </summary>
public sealed class ServiceIncidentDetector {
    private static readonly TimeSpan DedupeWindow = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NotificationHub _hub;
    private readonly ILogger<ServiceIncidentDetector> _logger;

    public ServiceIncidentDetector(IServiceScopeFactory scopeFactory, NotificationHub hub, ILogger<ServiceIncidentDetector> logger) {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _logger = logger;
    }

    public static bool IsFailure(SystemHttpLogEvent msg) {
        if (msg.StatusCode is 502 or 503 or 504) {
            return true;
        }

        if (msg.StatusCode == 0 && !string.IsNullOrWhiteSpace(msg.Error)) {
            return true;
        }

        if (msg.StatusCode >= 500 && msg.StatusCode < 600 && string.Equals(msg.Direction, "outbound", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        return false;
    }

    public async Task HandleAsync(SystemHttpLogEvent msg, CancellationToken ct) {
        var target = ResolveTarget(msg);
        if (string.IsNullOrWhiteSpace(target)) {
            return;
        }

        var bucket = BucketStart(msg.OccurredAtUtc, DedupeWindow);
        var statusBucket = ClassifyStatus(msg.StatusCode, msg.Error);
        var externalId = BuildExternalId(target, statusBucket, bucket);

        try {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
            var slaPolicy = scope.ServiceProvider.GetRequiredService<SlaPolicyService>();

            var existing = await db.WorkItems
                .Where(x => x.ExternalId == externalId && x.Source == "auto-incident")
                .Select(x => new { x.Id, x.Status })
                .FirstOrDefaultAsync(ct);

            if (existing is not null) {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var priority = ResolvePriority(msg.StatusCode, msg.Error);
            var slaMinutes = await slaPolicy.ResolveSlaMinutesAsync("bug", priority, ct);
            if (slaMinutes <= 0) {
                slaMinutes = priority == "P1" ? 60 : 240;
            }

            var title = BuildTitle(target, statusBucket, msg);
            var summary = BuildSummary(msg);

            var workItem = new WorkItem {
                Title = title,
                Type = "bug",
                Status = "Backlog",
                Priority = priority,
                Assignee = string.Empty,
                Source = "auto-incident",
                ExternalId = externalId,
                Tags = "auto,service-down",
                BugServiceName = target,
                BugEndpointPath = msg.Path,
                BugEndpointMethod = msg.Method,
                SlaMinutes = slaMinutes,
                SlaTargetAt = now.AddMinutes(slaMinutes),
                CreatedAt = now,
                UpdatedAt = now
            };

            db.WorkItems.Add(workItem);
            db.WorkItemStatusChanges.Add(new WorkItemStatusChange {
                WorkItemId = workItem.Id,
                FromStatus = null,
                ToStatus = workItem.Status,
                Actor = "auto-incident",
                // WorkItemStatusChange.Note is HasMaxLength(400) per TaskFlowDbContext mapping.
                Note = summary.Length > 400 ? summary[..400] : summary,
                ChangedAt = now
            });

            var notification = new WorkItemNotification {
                WorkItemId = workItem.Id,
                Type = "service-down",
                Channel = "in-app",
                Severity = priority == "P1" ? "critical" : "warning",
                // WorkItemNotification.Title is HasMaxLength(200) per TaskFlowDbContext mapping.
                Title = title.Length > 200 ? title[..200] : title,
                Message = summary,
                CreatedAt = now,
                // "Aksiyonlar" popover içeriği — maestro-console bell'inde
                // her sentinel notification'ında bu menü çıkar. Key'ler notification-api
                // NotificationActionInvokedConsumer ile aynı sözleşmeye uyar (gelecekte
                // support-ops-api kendi action consumer'ını yazarsa bu key'ler kullanılır).
                ActionsJson = SerializeIncidentActions()
            };
            db.WorkItemNotifications.Add(notification);

            try {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException) {
                // Race: another consumer instance inserted the same bucket. Treat as already handled.
                return;
            }

            try {
                await _hub.BroadcastAsync(
                    new WorkItemNotificationDto(
                        notification.Id,
                        workItem.Id,
                        notification.Type,
                        notification.Severity,
                        notification.Title,
                        notification.Message,
                        notification.Channel,
                        notification.CreatedAt,
                        notification.ReadAt,
                        ParseActionsForBroadcast(notification.ActionsJson)),
                    ct);
            }
            catch (Exception broadcastEx) {
                _logger.LogWarning(broadcastEx, "[auto-incident] WS broadcast failed for {ExternalId}", externalId);
            }

            _logger.LogWarning("[auto-incident] {Priority} task created for {Target} ({Status}) — externalId={ExternalId}", priority, target, statusBucket, externalId);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "[auto-incident] Failed to create incident for {Target}", target);
        }
    }

    /// <summary>
    /// Sentinel'in RECOVERED sinyali sonrası açık (Done/Closed/Resolved/Cancelled
    /// olmayan) tüm `auto-incident` WorkItem'larını verilen hedef servis için
    /// "Resolved" durumuna çeker. Her resolved kayıt için status history girişi
    /// + dev-support console'a recovery notification yayını yapılır.
    /// </summary>
    public async Task ResolveOpenIncidentsAsync(string target, string note, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(target)) {
            return;
        }

        try {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();

            var open = await db.WorkItems
                .Where(x => x.Source == "auto-incident"
                    && x.BugServiceName == target
                    && !ClosedStatuses.Contains(x.Status))
                .ToListAsync(ct);

            if (open.Count == 0) {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var notifications = new List<WorkItemNotification>(open.Count);

            foreach (var item in open) {
                var previousStatus = item.Status;
                item.Status = "Resolved";
                item.UpdatedAt = now;
                if (string.IsNullOrWhiteSpace(item.ResolutionNotes)) {
                    var trimmed = note.Length > 1000 ? note[..1000] : note;
                    item.ResolutionNotes = trimmed;
                }

                db.WorkItemStatusChanges.Add(new WorkItemStatusChange {
                    WorkItemId = item.Id,
                    FromStatus = previousStatus,
                    ToStatus = item.Status,
                    Actor = "auto-incident",
                    Note = note.Length > 400 ? note[..400] : note,
                    ChangedAt = now
                });

                var title = $"Recovered · {item.BugServiceName}";
                var notification = new WorkItemNotification {
                    WorkItemId = item.Id,
                    Type = "service-recovered",
                    Channel = "in-app",
                    Severity = "info",
                    Title = title.Length > 200 ? title[..200] : title,
                    Message = note.Length > 1000 ? note[..1000] : note,
                    CreatedAt = now
                };
                db.WorkItemNotifications.Add(notification);
                notifications.Add(notification);
            }

            try {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) {
                _logger.LogWarning(ex, "[auto-incident] Failed to persist recovery for {Target}", target);
                return;
            }

            for (var i = 0; i < open.Count; i++) {
                var item = open[i];
                var notification = notifications[i];
                try {
                    await _hub.BroadcastAsync(
                        new WorkItemNotificationDto(
                            notification.Id,
                            item.Id,
                            notification.Type,
                            notification.Severity,
                            notification.Title,
                            notification.Message,
                            notification.Channel,
                            notification.CreatedAt,
                            notification.ReadAt),
                        ct);
                }
                catch (Exception broadcastEx) {
                    _logger.LogWarning(broadcastEx, "[auto-incident] WS recovery broadcast failed for {WorkItemId}", item.Id);
                }
            }

            _logger.LogInformation("[auto-incident] Resolved {Count} open auto-bug(s) for {Target} (recovery).", open.Count, target);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "[auto-incident] Failed to resolve incidents for {Target}", target);
        }
    }

    private static readonly string[] ClosedStatuses = new[] { "Done", "Closed", "Resolved", "Cancelled" };

    private static string ResolveTarget(SystemHttpLogEvent msg) {
        if (!string.IsNullOrWhiteSpace(msg.TargetService)) {
            return msg.TargetService.Trim();
        }

        if (!string.IsNullOrWhiteSpace(msg.Service) && string.Equals(msg.Direction, "outbound", StringComparison.OrdinalIgnoreCase)) {
            return msg.Service.Trim();
        }

        return msg.Service?.Trim() ?? string.Empty;
    }

    private static string ResolvePriority(int statusCode, string? error) {
        if (statusCode is 502 or 503 or 504) {
            return "P1";
        }

        if (statusCode == 0 && !string.IsNullOrWhiteSpace(error)) {
            return "P1";
        }

        return "P2";
    }

    private static string ClassifyStatus(int statusCode, string? error) {
        return statusCode switch {
            502 => "502-bad-gateway",
            503 => "503-unavailable",
            504 => "504-timeout",
            0 when !string.IsNullOrWhiteSpace(error) => "connection-failed",
            >= 500 and < 600 => $"{statusCode}-upstream-error",
            _ => "unknown"
        };
    }

    private static DateTimeOffset BucketStart(DateTimeOffset at, TimeSpan window) {
        var ticks = window.Ticks;
        return ticks <= 0 ? at : new DateTimeOffset(at.Ticks - (at.Ticks % ticks), at.Offset);
    }

    private static string BuildExternalId(string target, string statusBucket, DateTimeOffset bucketStart) {
        // Stable, short hash so the ExternalId fits in 64 chars.
        var seed = $"auto-incident|{target.ToLowerInvariant()}|{statusBucket}|{bucketStart:yyyyMMddHHmm}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var hex = Convert.ToHexString(hash, 0, 12);
        return $"auto-{hex}".ToLowerInvariant();
    }

    // Sentinel notification'larında 2 aksiyon: onayla (yoksay) ve Maestro'ya gönder.
    // 2026-05-16: Eskiden Codex/Anthropic/Gemini için 3 ayrı buton vardı — kullanıcı
    // hangisinin doğru olduğunu bilmek zorunda kalmasın diye tek "Maestro'ya gönder"
    // butonu altında birleştirildi. notification-api consumer `escalate_to_maestro`
    // key'ini IProviderRouter benzeri sıralama ile uygun ajana yönlendiriyor
    // (Anthropic configured ise o, değilse Gemini, sonunda Codex PR fallback).
    private static readonly WorkItemNotificationAction[] DefaultIncidentActions = new[] {
        new WorkItemNotificationAction("acknowledge", "Onayla / Yoksay"),
        new WorkItemNotificationAction("escalate_to_maestro", "Maestro'ya gönder", "primary"),
    };

    private static string SerializeIncidentActions() {
        return JsonSerializer.Serialize(DefaultIncidentActions);
    }

    private static IReadOnlyList<WorkItemNotificationActionDto>? ParseActionsForBroadcast(string? json) {
        if (string.IsNullOrWhiteSpace(json)) {
            return null;
        }
        try {
            var parsed = JsonSerializer.Deserialize<WorkItemNotificationActionDto[]>(json);
            return parsed == null || parsed.Length == 0 ? null : parsed;
        }
        catch {
            return null;
        }
    }

    private static string BuildTitle(string target, string statusBucket, SystemHttpLogEvent msg) {
        var verb = string.IsNullOrWhiteSpace(msg.Method) ? string.Empty : $"{msg.Method} ";
        var path = string.IsNullOrWhiteSpace(msg.Path) ? string.Empty : msg.Path;
        var trimmed = path.Length > 80 ? path[..80] + "…" : path;
        var bracket = string.IsNullOrWhiteSpace(trimmed) ? string.Empty : $" {verb}{trimmed}";
        var label = $"{target} · {statusBucket}{bracket}";
        return label.Length > 240 ? label[..240] : label;
    }

    private static string BuildSummary(SystemHttpLogEvent msg) {
        var parts = new List<string> {
            $"status={msg.StatusCode}",
            $"durationMs={msg.DurationMs}"
        };
        if (!string.IsNullOrWhiteSpace(msg.TargetUrl)) {
            parts.Add($"url={msg.TargetUrl}");
        }
        if (!string.IsNullOrWhiteSpace(msg.Service)) {
            parts.Add($"observer={msg.Service}");
        }
        if (!string.IsNullOrWhiteSpace(msg.Error)) {
            var err = msg.Error.Length > 240 ? msg.Error[..240] : msg.Error;
            parts.Add($"error={err}");
        }

        var joined = string.Join(" | ", parts);
        return joined.Length > 1000 ? joined[..1000] : joined;
    }
}
