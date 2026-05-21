namespace SupportOpsApi.Models;

public class WorkItemComment {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public Guid WorkItemId { get; set; }
    public WorkItem? WorkItem { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Format { get; set; } = "text"; // text | html (sanitized)
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
