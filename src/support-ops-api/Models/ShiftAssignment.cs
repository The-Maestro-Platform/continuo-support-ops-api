namespace SupportOpsApi.Models;

public class ShiftAssignment {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public string Agent { get; set; } = string.Empty;
    public DateTimeOffset Start { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset End { get; set; } = DateTimeOffset.UtcNow.AddHours(8);
    public string Channel { get; set; } = "chat";
}
