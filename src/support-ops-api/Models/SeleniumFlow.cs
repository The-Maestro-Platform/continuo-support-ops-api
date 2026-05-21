namespace SupportOpsApi.Models;

/// <summary>
/// Named, ordered sequence of Selenium tests. When a flow is queued, one
/// <see cref="SeleniumRun"/> is created per <see cref="SeleniumFlowStep"/> and they execute
/// in <see cref="SeleniumFlowStep.Order"/> order. If a step is marked
/// <see cref="SeleniumFlowStep.StopOnFailure"/>, the remaining queued runs for that flow
/// batch are cancelled once the step fails.
/// </summary>
public class SeleniumFlow {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedBy { get; set; } = "System";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<SeleniumFlowStep> Steps { get; set; } = new List<SeleniumFlowStep>();
}

public class SeleniumFlowStep {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public Guid FlowId { get; set; }
    public int Order { get; set; }
    public Guid TestId { get; set; }
    public bool StopOnFailure { get; set; } = true;

    public SeleniumFlow? Flow { get; set; }
    public SeleniumTest? Test { get; set; }
}
