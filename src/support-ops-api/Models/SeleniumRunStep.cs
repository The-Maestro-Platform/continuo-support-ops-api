namespace SupportOpsApi.Models;

public class SeleniumRunStep {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public Guid RunId { get; set; }
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending | passed | failed | skipped
    public int DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ScreenshotRef { get; set; }
    public string? ConsoleLog { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    public SeleniumRun? Run { get; set; }
}
