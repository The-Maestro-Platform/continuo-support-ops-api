namespace SupportOpsApi.Models;

// Free-form feedback from pilot operators. Kept separate from Incident so ops
// can triage "nice to have" UX ideas without polluting the SLA queue. Status
// machine: new → triaged → resolved | dismissed. Pilot site code + operator
// name are stored verbatim so the SupportOps dashboard can group by location.
public class PilotFeedback {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public string PilotSite { get; set; } = string.Empty;
    public string Category { get; set; } = "general"; // general | bug | ux | performance | safety
    public int Severity { get; set; } = 3;            // 1 (critical) .. 5 (trivial)
    public int Satisfaction { get; set; } = 3;        // 1..5 CSAT rating
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? SubmittedBy { get; set; }
    public string? SubmittedRole { get; set; }
    public string? ContactEmail { get; set; }
    public string Status { get; set; } = "new";       // new | triaged | resolved | dismissed
    public string? Resolution { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAtUtc { get; set; }
}
