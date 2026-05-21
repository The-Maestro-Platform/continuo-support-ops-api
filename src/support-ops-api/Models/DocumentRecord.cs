namespace SupportOpsApi.Models;

public class DocumentRecord {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "Analysis"; // Runbook, Analysis, Technical Spec, Postmortem
    public string Status { get; set; } = "Draft";
    public string Owner { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty; // comma-separated
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
    public string? Link { get; set; }
    public ICollection<DocumentLink> WorkItemLinks { get; set; } = new List<DocumentLink>();
}
