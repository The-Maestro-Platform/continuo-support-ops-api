using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace SupportOpsApi.Services;

public sealed class DmsApiClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IMemoryCache cache) {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = true
    };

    public async Task<DmsItemDto?> GetItemAsync(Guid id, CancellationToken ct) {
        var cacheKey = $"dms:item:{id}";
        if (cache.TryGetValue(cacheKey, out DmsItemDto? cached)) {
            return cached;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"dms/items/{id}");
        ForwardAuth(request);

        var client = httpClientFactory.CreateClient("support-ops-dms");
        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<DmsItemDto>(JsonOptions, ct);
        if (item is not null) {
            cache.Set(cacheKey, item, TimeSpan.FromMinutes(1));
        }
        return item;
    }

    public async Task<DmsItemDto> CreateItemAsync(CreateDmsItemRequest request, CancellationToken ct) {
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(request.File.OpenReadStream());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(request.File.ContentType) ? "application/octet-stream" : request.File.ContentType);
        content.Add(fileContent, "file", request.File.FileName);
        content.Add(new StringContent(request.Title), "title");
        content.Add(new StringContent(request.ServiceName), "serviceName");
        if (!string.IsNullOrWhiteSpace(request.Description)) {
            content.Add(new StringContent(request.Description), "description");
        }
        if (!string.IsNullOrWhiteSpace(request.Note)) {
            content.Add(new StringContent(request.Note), "note");
        }
        if (request.Tags?.Count > 0) {
            content.Add(new StringContent(JsonSerializer.Serialize(request.Tags, JsonOptions)), "tags");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "dms/items") {
            Content = content
        };
        ForwardAuth(httpRequest);

        var client = httpClientFactory.CreateClient("support-ops-dms");
        using var response = await client.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var item = await response.Content.ReadFromJsonAsync<DmsItemDto>(JsonOptions, ct);
        if (item is null) {
            throw new InvalidOperationException("DMS item response missing.");
        }
        cache.Set($"dms:item:{item.Id}", item, TimeSpan.FromMinutes(1));
        return item;
    }

    private void ForwardAuth(HttpRequestMessage request) {
        var auth = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(auth)) {
            request.Headers.TryAddWithoutValidation("Authorization", auth);
        }
    }
}

public sealed record CreateDmsItemRequest(
    IFormFile File,
    string Title,
    string ServiceName,
    string? Description,
    string? Note,
    IReadOnlyList<DmsTagDto>? Tags);

public sealed record DmsTagDto(string Key, string? Value);

public sealed record DmsItemDto(
    Guid Id,
    string Title,
    string ServiceName,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid CurrentVersionId,
    Guid CurrentFileId,
    IReadOnlyList<DmsTagDto> Tags,
    IReadOnlyList<DmsItemVersionDto> Versions);

public sealed record DmsItemVersionDto(
    Guid Id,
    int VersionNumber,
    Guid FileId,
    string FileName,
    string ContentType,
    long Length,
    string UploadedBy,
    DateTimeOffset UploadedAt,
    string? Note,
    string DownloadUrl);
