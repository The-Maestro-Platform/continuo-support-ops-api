namespace SupportOpsApi.Models;

public class WorkItemNotification {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public Guid WorkItemId { get; set; }
    public WorkItem? WorkItem { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
    // JSON array of {key, label, kind?} — maestro-console bell'inde "Aksiyonlar"
    // popover'ında listelenir. Null/empty ise popover gösterilmez. Pattern
    // notification-api'nin Notification.ActionsJson alanı ile birebir aynı.
    public string? ActionsJson { get; set; }
}

/// <summary>
/// WorkItemNotification action item — JSON serialize edilip ActionsJson kolonuna yazılır.
/// Frontend NotificationBell.NotificationAction ile birebir uyumlu.
/// </summary>
public sealed record WorkItemNotificationAction(string Key, string Label, string? Kind = null);
