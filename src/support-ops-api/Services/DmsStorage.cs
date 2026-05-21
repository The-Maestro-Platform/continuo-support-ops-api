using System.Security.Cryptography;
using SupportOpsApi.Models;

namespace SupportOpsApi.Services;

public class DmsStorage {
    private readonly string _root;

    public DmsStorage(IConfiguration configuration, IWebHostEnvironment environment) {
        _root =
            configuration["DMS__ROOT"] ??
            configuration["DMS_ROOT"] ??
            configuration["DMS:ROOT"] ??
            Path.Combine(environment.ContentRootPath, "fileserver", "dms");
    }

    public string ResolveAbsolutePath(string storagePath)
        => Path.IsPathRooted(storagePath) ? storagePath : Path.Combine(_root, storagePath);

    public async Task<(DmsFile file, string absolutePath)> SaveAsync(IFormFile upload, CancellationToken ct) {
        if (upload.Length <= 0) {
            throw new InvalidOperationException("Empty file");
        }

        var now = DateTimeOffset.UtcNow;
        var relative = Path.Combine(now.ToString("yyyy"), now.ToString("MM"), Ulid.NewUlid().ToGuid().ToString("N"));
        var absolute = ResolveAbsolutePath(relative);

        var dir = Path.GetDirectoryName(absolute);
        if (!string.IsNullOrWhiteSpace(dir)) {
            Directory.CreateDirectory(dir);
        }

        await using (var stream = File.Create(absolute)) {
            await upload.CopyToAsync(stream, ct);
        }

        string sha256;
        await using (var read = File.OpenRead(absolute)) {
            var hash = await SHA256.HashDataAsync(read, ct);
            sha256 = Convert.ToHexString(hash).ToLowerInvariant();
        }

        var entity = new DmsFile {
            FileName = Path.GetFileName(upload.FileName),
            ContentType = string.IsNullOrWhiteSpace(upload.ContentType) ? "application/octet-stream" : upload.ContentType,
            Length = upload.Length,
            Sha256 = sha256,
            StoragePath = relative.Replace('\\', '/'),
            CreatedAt = now
        };

        return (entity, absolute);
    }
}

