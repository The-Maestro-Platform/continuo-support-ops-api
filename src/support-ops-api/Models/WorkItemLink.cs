namespace SupportOpsApi.Models;

public class WorkItemLink {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public Guid WorkItemId { get; set; }
    public Guid RelatedWorkItemId { get; set; }
    public string Relation { get; set; } = "relates to";
    public WorkItem? RelatedWorkItem { get; set; }
}
