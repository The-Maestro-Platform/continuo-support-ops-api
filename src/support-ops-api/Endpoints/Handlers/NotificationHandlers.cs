using System.Text.Json;
using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SupportOpsApi.Data;
using SupportOpsApi.Endpoints.Contracts;
using Continuo.Observability.Attributes;
using Continuo.Shared.Contracts;

namespace SupportOpsApi.Endpoints.Handlers;

public static class NotificationHandlers {
    [ContinuoProxyMethod("ui")]
    public static async Task<Ok<IEnumerable<WorkItemNotificationDto>>> GetNotifications(
        TaskFlowDbContext db,
        int? take,
        bool? unreadOnly,
        CancellationToken ct) {
        var limit = Math.Clamp(take ?? 20, 1, 200);
        var query = db.WorkItemNotifications.AsQueryable();
        if (unreadOnly == true) {
            query = query.Where(x => x.ReadAt == null);
        }

        // ActionsJson string olarak çekilir, response'a map ederken parse edilir.
        // EF projection JSON deserialize yapmıyor; Select sonrası in-memory hayli ucuz.
        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .Select(x => new {
                x.Id, x.WorkItemId, x.Type, x.Severity, x.Title, x.Message,
                x.Channel, x.CreatedAt, x.ReadAt, x.ActionsJson
            })
            .ToListAsync(ct);

        var items = rows.Select(x => new WorkItemNotificationDto(
            x.Id,
            x.WorkItemId,
            x.Type,
            x.Severity,
            x.Title,
            x.Message,
            x.Channel,
            x.CreatedAt,
            x.ReadAt,
            ParseActions(x.ActionsJson))).ToList();

        return TypedResults.Ok(items.AsEnumerable());
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<NoContent, NotFound>> MarkNotificationRead(
        Guid id,
        TaskFlowDbContext db,
        CancellationToken ct) {
        var entity = await db.WorkItemNotifications.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) {
            return TypedResults.NotFound();
        }

        if (entity.ReadAt is null) {
            entity.ReadAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return TypedResults.NoContent();
    }

    public record RespondNotificationRequest(string Decision);

    /// <summary>
    /// maestro-console bell'inden "Aksiyonlar" popover'ında bir seçenek
    /// işaretlendiğinde tetiklenir. notification-api'deki NotificationController.Respond
    /// ile aynı sözleşme: decision = action key (acknowledge/escalate_to_codex/...).
    ///
    /// İmplementasyon: status'ü normalize edip kaydet, sonra notification-api'nin
    /// dinlediği <see cref="NotificationActionInvokedEvent"/>'i publish et — böylece
    /// codex tetikleme, AI provider çağrıları aynı consumer'dan geçer, kod
    /// duplikasyonu yok. NotificationId alanına WorkItemNotification.Id'yi Ulid
    /// olarak geçiyoruz (mevcut entity Guid, Ulid.NewUlid().ToGuid() pattern'i).
    /// </summary>
    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<WorkItemNotificationDto>, NotFound>> RespondNotification(
        Guid id,
        RespondNotificationRequest req,
        TaskFlowDbContext db,
        IPublishEndpoint publisher,
        CancellationToken ct) {
        var entity = await db.WorkItemNotifications.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) {
            return TypedResults.NotFound();
        }

        // 2026-05-16: Eskiden NormalizeDecision raw key'i ("escalate_to_codex" gibi)
        // generic "escalated" status'üne sıkıştırıyordu — consumer hangi ajana yöneleceğini
        // bilemiyordu (Codex Runs hep boş kalıyordu). Şimdi raw decision'u event'e olduğu
        // gibi geçiyoruz; consumer kendi `acknowledged`/`escalate_to_*` branch'lerini
        // raw key üzerinden yapar. Status normalize yine de yapılıyor — entity.Status
        // alanı kısa enum için lazım ama event Decision alanı raw kalır.
        var rawDecision = (req.Decision ?? string.Empty).Trim();
        var normalizedStatus = NormalizeDecision(rawDecision);
        // Respond olunca okundu sayılır.
        if (entity.ReadAt is null) {
            entity.ReadAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);

        // notification-api consumer'ı tetiklemek için Ulid'e çevir; Guid-tabanlı
        // tablodan Ulid'e geçişte information loss olmasın diye Ulid.Parse mantığı
        // kullanılamaz (Guid 16 byte, Ulid 16 byte ama farklı encoding). Geçici
        // çözüm: Guid byte'larından Ulid kuralım — collision risksiz, sadece
        // sözleşme uyumu için.
        var notificationUlid = new Ulid(id.ToByteArray());

        // CallbackKey = "work-item-notification:{type}" ile consumer ayrıştırabilir.
        // Payload olarak workItemId + entity context'i veriyoruz; codex prompt'unda
        // yer alacak.
        var callbackPayload = JsonSerializer.Serialize(new {
            kind = "work-item-notification",
            workItemNotificationId = entity.Id,
            workItemId = entity.WorkItemId,
            type = entity.Type,
            severity = entity.Severity,
            title = entity.Title,
            message = entity.Message,
        });

        // Decision olarak raw action key'i geçiyoruz ("escalate_to_codex",
        // "escalate_to_maestro", "acknowledge", ...). Consumer kendi normalize'ını
        // yapar; normalizedStatus sadece status enum'ı için kullanıldı (entity.Status
        // gerekirse buradan set edilir).
        _ = normalizedStatus;
        await publisher.Publish(new NotificationActionInvokedEvent(
            NotificationId: notificationUlid,
            CallbackKey: $"work-item-notification:{entity.Type}",
            CallbackPayloadJson: callbackPayload,
            Decision: rawDecision,
            OccurredAt: DateTime.UtcNow), ct);

        var dto = new WorkItemNotificationDto(
            entity.Id,
            entity.WorkItemId,
            entity.Type,
            entity.Severity,
            entity.Title,
            entity.Message,
            entity.Channel,
            entity.CreatedAt,
            entity.ReadAt,
            ParseActions(entity.ActionsJson));

        return TypedResults.Ok(dto);
    }

    private static string NormalizeDecision(string? raw) {
        if (string.IsNullOrWhiteSpace(raw)) return "rejected";
        var t = raw.Trim();
        if (string.Equals(t, "acknowledge", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t, "ack", StringComparison.OrdinalIgnoreCase)) {
            return "acknowledged";
        }
        if (t.StartsWith("escalate", StringComparison.OrdinalIgnoreCase)) return "escalated";
        if (string.Equals(t, "confirm", StringComparison.OrdinalIgnoreCase)) return "confirmed";
        if (string.Equals(t, "reject", StringComparison.OrdinalIgnoreCase)) return "rejected";
        return t.Length > 32 ? t[..32] : t;
    }

    private static IReadOnlyList<WorkItemNotificationActionDto>? ParseActions(string? json) {
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
}
