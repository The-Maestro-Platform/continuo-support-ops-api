namespace SupportOpsApi.Models;

public class SystemHttpLogEntry {
    public Guid Id { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string Service { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }

    public string? TenantId { get; set; }
    public string? ClientApp { get; set; }
    public string? RemoteIp { get; set; }
    public string? ClientIp { get; set; }
    public string? UserAgent { get; set; }
    public string? InitiatorUserId { get; set; }
    public string? InitiatorUserName { get; set; }
    public string? InitiatorUserEmail { get; set; }
    public string? ClientGeoCountryCode { get; set; }
    public string? ClientGeoRegion { get; set; }
    public string? ClientGeoCity { get; set; }
    public double? ClientGeoLatitude { get; set; }
    public double? ClientGeoLongitude { get; set; }
    public string? TargetService { get; set; }
    public string? TargetUrl { get; set; }
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public string? CorrelationId { get; set; }
    public string? RequestHeadersJson { get; set; }
    public string? ResponseHeadersJson { get; set; }
    public string? RequestBody { get; set; }
    public string? ResponseBody { get; set; }
    public string? Error { get; set; }
}
