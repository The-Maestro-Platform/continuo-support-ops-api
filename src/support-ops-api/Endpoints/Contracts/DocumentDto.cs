namespace SupportOpsApi.Endpoints.Contracts;

public record DocumentDto(
    Guid Id,
    string Title,
    string Category,
    string Status,
    string Owner,
    DateTimeOffset LastUpdated,
    IEnumerable<string> Tags,
    IEnumerable<Guid> RelatedWorkItems,
    string? Link);
