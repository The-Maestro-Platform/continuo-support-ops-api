namespace SupportOpsApi.Models;

public class KnowledgeArticle {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public string ExternalId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public ICollection<KnowledgeRevision> Revisions { get; set; } = new List<KnowledgeRevision>();
}
