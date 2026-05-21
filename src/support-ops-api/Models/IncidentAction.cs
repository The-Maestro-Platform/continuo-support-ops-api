namespace SupportOpsApi.Models;

public class IncidentAction {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public Guid IncidentId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
