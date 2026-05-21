namespace SupportOpsApi.Endpoints.Contracts;

public record WorkItemAttachmentDto(
    Guid Id,
    Guid FileId,
    string FileName,
    string ContentType,
    long Length,
    string Kind,
    string Url,
    DateTimeOffset UploadedAt,
    string UploadedBy);
