namespace SupportOpsApi.Endpoints.Contracts;

public record UpsertWorkItemRequest(
    string Title,
    string Type,
    string? Status,
    string? Priority,
    string? Assignee,
    string? Source,
    Guid? BugServiceId,
    string? BugServiceName,
    Guid? BugEndpointId,
    string? BugEndpointPath,
    string? BugEndpointMethod,
    string? ExternalId,
    string? ResolutionNotes,
    int? SlaMinutes,
    DateTimeOffset? SlaTargetAt,
    GithubInfo? Github,
    IEnumerable<string>? Tags);
