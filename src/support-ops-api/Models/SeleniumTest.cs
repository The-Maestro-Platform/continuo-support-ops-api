namespace SupportOpsApi.Models;

/// <summary>
/// A catalog entry describing a Selenium test. Two kinds are supported:
///   - "code"     → references a compiled xUnit test (<see cref="CodeFullyQualifiedName"/>)
///                  that the runner executes via <c>dotnet test --filter</c>.
///   - "scenario" → dynamic, step-based test whose steps are stored as JSON in
///                  <see cref="ScenarioJson"/>; the runner interprets the steps using the
///                  same helpers that <c>TestBase</c> exposes.
/// </summary>
public class SeleniumTest {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public string Code { get; set; } = string.Empty;   // unique slug (e.g. qrmenu-order-flow)
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Kind { get; set; } = "code";         // code | scenario
    public string? CodeFullyQualifiedName { get; set; }
    public string? ScenarioJson { get; set; }
    public string Tags { get; set; } = string.Empty;   // csv
    public int TimeoutSeconds { get; set; } = 600;
    public bool IsActive { get; set; } = true;
    public string CreatedBy { get; set; } = "System";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
