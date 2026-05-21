namespace SupportOpsApi.Models;

public class WorkItem {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public string ExternalId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = "task"; // task | bug | analysis
    public string Status { get; set; } = "Backlog";
    public string Priority { get; set; } = "P3";
    public string Assignee { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? GithubRepo { get; set; }
    public string? GithubBranch { get; set; }
    public string? GithubCommit { get; set; }
    public string? GithubPullRequest { get; set; }
    public string Tags { get; set; } = string.Empty; // comma-separated

    // Optional bug metadata (service/endpoint)
    public Guid? BugServiceId { get; set; }
    public string? BugServiceName { get; set; }
    public Guid? BugEndpointId { get; set; }
    public string? BugEndpointPath { get; set; }
    public string? BugEndpointMethod { get; set; }
    public string? ResolutionNotes { get; set; }
    public int? SlaMinutes { get; set; }
    public DateTimeOffset? SlaTargetAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<WorkItemLink> Links { get; set; } = new List<WorkItemLink>();
    public ICollection<DocumentLink> DocumentLinks { get; set; } = new List<DocumentLink>();
    public ICollection<WorkItemComment> Comments { get; set; } = new List<WorkItemComment>();
    public ICollection<WorkItemAttachment> Attachments { get; set; } = new List<WorkItemAttachment>();
    public ICollection<WorkItemStatusChange> StatusHistory { get; set; } = new List<WorkItemStatusChange>();
}
