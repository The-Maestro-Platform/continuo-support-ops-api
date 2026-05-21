namespace SupportOpsApi.Models;

public class KnowledgeRevision {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public Guid KnowledgeArticleId { get; set; }
    public int Version { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
