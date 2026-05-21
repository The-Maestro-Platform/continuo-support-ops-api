namespace SupportOpsApi.Models;

/// <summary>
/// A Selenium runner worker instance. Runners register themselves on boot, emit
/// heartbeats, and poll the claim endpoint for queued runs. Both server-hosted and
/// developer-local runners share this model — the difference is reflected in
/// <see cref="Location"/> and the runner's connectivity.
/// </summary>
public class SeleniumRunner {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public string Name { get; set; } = string.Empty;      // human-readable ("mert-laptop", "gke-runner-1")
    public string Location { get; set; } = "local";      // local | server
    /// <summary>
    /// Hangi platform ortamına (dev / staging / prod) ait runner olduğunu işaretler. Boş bırakılırsa
    /// "any" sayılır ve hangi ortamdan kuyruğa alınmış olursa olsun runner'ı eşleştirebilir (eski davranış).
    /// Aksi halde ClaimNextRun yalnız aynı ortama ait runları o runner'a verir; örnek:
    /// <c>dev-selenium-runner</c> sadece <c>dev.example.local</c> ekranından enqueue edilen run'ları çeker.
    /// </summary>
    public string Environment { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Status { get; set; } = "idle";        // idle | busy | offline
    public string Capabilities { get; set; } = string.Empty; // csv
    public Guid? CurrentRunId { get; set; }
    /// <summary>
    /// Maximum number of concurrent test executions this runner can handle. Clamped to [1, 8].
    /// 1 = classic single-threaded behavior. Runner worker enforces this via a semaphore and
    /// the UI surfaces it on the runner panel (Selenium Runner tab).
    /// </summary>
    public int MaxParallel { get; set; } = 1;
    public DateTimeOffset LastHeartbeatAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
