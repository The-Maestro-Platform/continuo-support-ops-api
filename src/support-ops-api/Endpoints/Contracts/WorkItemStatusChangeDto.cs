namespace SupportOpsApi.Endpoints.Contracts;

public record WorkItemStatusChangeDto(
    Guid Id,
    string? From,
    string To,
    string Actor,
    DateTimeOffset At,
    string? Note);
