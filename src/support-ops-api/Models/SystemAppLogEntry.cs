namespace SupportOpsApi.Models;

public class SystemAppLogEntry {
    public Guid Id { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string Service { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? MessageTemplate { get; set; }
    public string? SourceContext { get; set; }
    public string? Exception { get; set; }
    public string? CorrelationId { get; set; }
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public string? TenantId { get; set; }
    public string? UserName { get; set; }
    public string? PropertiesJson { get; set; }
}
