namespace SupportOpsApi.Models;

public class WorkItemAttachment {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public Guid WorkItemId { get; set; }
    public WorkItem? WorkItem { get; set; }
    public Guid? FileId { get; set; }
    public DmsFile? File { get; set; }
    public Guid? DmsItemId { get; set; }
    public string Kind { get; set; } = "attachment"; // attachment | screenshot
    public string UploadedBy { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
