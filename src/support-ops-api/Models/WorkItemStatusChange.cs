namespace SupportOpsApi.Models;

public class WorkItemStatusChange {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public Guid WorkItemId { get; set; }
    public WorkItem? WorkItem { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
}
