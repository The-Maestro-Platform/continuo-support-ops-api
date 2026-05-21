namespace SupportOpsApi.Models;

public class UiErrorLogEntry {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string App { get; set; } = string.Empty;
    public string Source { get; set; } = "client";
    public string Level { get; set; } = "error";
    public string Message { get; set; } = string.Empty;
    public string? Stack { get; set; }
    public string? Url { get; set; }
    public string? Path { get; set; }
    public string? UserAgent { get; set; }
    public string? TenantSlug { get; set; }
    public string? ClientApp { get; set; }
    public string? UserId { get; set; }
    public string? UserLogin { get; set; }
    public string? SessionId { get; set; }
    public string? CorrelationId { get; set; }
    public string? TraceId { get; set; }
    public string? Release { get; set; }
    public string? TagsJson { get; set; }
    public string? ExtraJson { get; set; }
}
