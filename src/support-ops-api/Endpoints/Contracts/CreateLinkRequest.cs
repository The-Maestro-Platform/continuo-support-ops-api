namespace SupportOpsApi.Endpoints.Contracts;

public record CreateLinkRequest(Guid RelatedWorkItemId, string Relation);
