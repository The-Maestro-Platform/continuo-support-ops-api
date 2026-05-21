namespace SupportOpsApi.Endpoints.Contracts;

public record WorkItemNotificationDto(
    Guid Id,
    Guid WorkItemId,
    string Type,
    string Severity,
    string Title,
    string Message,
    string Channel,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt,
    IReadOnlyList<WorkItemNotificationActionDto>? Actions = null);

/// <summary>
/// "Aksiyonlar" popover item — frontend NotificationBell.NotificationAction
/// ile birebir uyumlu. Key kalıcı (i18n'siz), Label görünür Türkçe etiket.
/// Kind: default | primary | danger.
/// </summary>
public record WorkItemNotificationActionDto(string Key, string Label, string? Kind = null);
