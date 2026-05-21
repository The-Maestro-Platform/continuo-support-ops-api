namespace SupportOpsApi.Models;

public class DbEventHistoryEntry {
    public Guid Id { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string Service { get; set; } = string.Empty;
    public string DbContext { get; set; } = string.Empty;
    public string? Database { get; set; }
    public int AddedCount { get; set; }
    public int ModifiedCount { get; set; }
    public int DeletedCount { get; set; }
    public long? DurationMs { get; set; }
    public string? CorrelationId { get; set; }
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public string? InitiatorUserId { get; set; }
    public string? InitiatorUserName { get; set; }
    public string? InitiatorUserEmail { get; set; }
    public string? EntitiesJson { get; set; }
    public string? Error { get; set; }
}
