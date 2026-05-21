namespace SupportOpsApi.Endpoints.Contracts;

public record WorkItemRelation(Guid Id, string Relation, string Type, string Title, Guid LinkId);
