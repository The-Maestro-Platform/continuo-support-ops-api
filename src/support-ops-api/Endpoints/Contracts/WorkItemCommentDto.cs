namespace SupportOpsApi.Endpoints.Contracts;

public record WorkItemCommentDto(Guid Id, string Author, string Text, string Format, DateTimeOffset CreatedAt);
