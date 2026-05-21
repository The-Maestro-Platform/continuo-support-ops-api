namespace SupportOpsApi.Endpoints.Contracts;

public record UpsertDocumentRequest(
    string Title,
    string? Category,
    string? Status,
    string? Owner,
    IEnumerable<string>? Tags,
    IEnumerable<Guid>? RelatedWorkItemIds,
    string? Link);
