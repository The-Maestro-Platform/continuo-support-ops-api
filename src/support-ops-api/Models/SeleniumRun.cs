namespace SupportOpsApi.Models;

/// <summary>
/// A single execution of a <see cref="SeleniumTest"/>. Runs are placed into a FIFO queue
/// (<see cref="Status"/> = <c>queued</c>), claimed by a runner (<c>claimed</c> → <c>running</c>),
/// and terminate in <c>passed</c>, <c>failed</c>, <c>cancelled</c> or <c>timeout</c>.
///
/// When a run fails, the support-ops-api automatically calls the auto-bug pipeline and
/// stores the resulting work item id in <see cref="AutoBugWorkItemId"/>.
/// </summary>
public class SeleniumRun {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public Guid TestId { get; set; }
    public Guid? FlowId { get; set; }
    public int? FlowStepOrder { get; set; }
    public Guid? FlowBatchId { get; set; }          // groups runs queued together for the same flow invocation
    public Guid? RunnerId { get; set; }
    public Guid? TargetRunnerId { get; set; }       // if set, only this runner may claim the run
    /// <summary>
    /// Run'ı enqueue eden UI'nin ortamı (dev/staging/prod). UI hostname'inden
    /// (`dev-tenant1...`, `staging-...`) çıkarılır ve buraya yazılır. ClaimNextRun
    /// `Environment` set'li bir runner'a yalnız aynı ortama ait run'ları verir.
    /// Boş ise herhangi bir runner çekebilir (geriye dönük davranış).
    /// </summary>
    public string? Environment { get; set; }
    public string Status { get; set; } = "queued"; // queued | claimed | running | passed | failed | cancelled | timeout
    public int Priority { get; set; } = 3;          // 1 highest, 5 lowest
    public int AttemptCount { get; set; }
    public string QueuedBy { get; set; } = "System";
    public DateTimeOffset QueuedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClaimedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public int DurationMs { get; set; }
    public string? Stdout { get; set; }
    public string? Stderr { get; set; }
    public string? FailureMessage { get; set; }
    public string? ScreenshotRef { get; set; }
    public string? EnvironmentJson { get; set; }    // JSON dict of env overrides (e.g. ADMIN_BASE_URL, WEB_BASE_URL)
    public Guid? AutoBugWorkItemId { get; set; }

    public SeleniumTest? Test { get; set; }
    public SeleniumFlow? Flow { get; set; }
    public SeleniumRunner? Runner { get; set; }
    public List<SeleniumRunStep> Steps { get; set; } = [];
}
