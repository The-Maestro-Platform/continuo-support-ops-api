namespace SupportOpsApi.Models;

public class SupportTask {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public string Title { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Status { get; set; } = "Todo";
    public DateTimeOffset DueAt { get; set; } = DateTimeOffset.UtcNow.AddDays(1);
}
