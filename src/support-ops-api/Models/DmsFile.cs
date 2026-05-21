namespace SupportOpsApi.Models;

public class DmsFile {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long Length { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty; // absolute or app-relative path
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
