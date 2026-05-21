namespace SupportOpsApi.Models;

public class DocumentLink {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public Guid DocumentRecordId { get; set; }
    public Guid WorkItemId { get; set; }
}
