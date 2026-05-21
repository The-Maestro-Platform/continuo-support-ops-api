namespace SupportOpsApi.Models;

public class Alert {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public string Source { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
    public string Message { get; set; } = string.Empty;
    public bool Acknowledged { get; set; } = false;
    public DateTimeOffset RaisedAt { get; set; } = DateTimeOffset.UtcNow;
}
