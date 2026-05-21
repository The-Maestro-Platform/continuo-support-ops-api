namespace SupportOpsApi.Models;

public class Incident {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public string ExternalId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Priority { get; set; } = "P3";
    public string Status { get; set; } = "Open";
    public string Owner { get; set; } = string.Empty;
    public int SlaMinutes { get; set; }
    public ICollection<IncidentAction> Actions { get; set; } = new List<IncidentAction>();
}
