using Microsoft.Extensions.Logging;

namespace MediaArchive.Services;

public class CoverCacheService(
    HttpClient httpClient,
    string storageRoot,
    ILogger<CoverCacheService> logger) : ICoverCache
{
    public const string UrlBase = "covers://c";

    private readonly string _storageRoot = storageRoot;

    public async Task<string?> TryCacheAsync(string? imageUrl, string? externalSource,
        string? externalId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) ||
            string.IsNullOrWhiteSpace(externalSource) ||
            string.IsNullOrWhiteSpace(externalId))
            return null;

        try
        {
            using var response = await httpClient.GetAsync(imageUrl, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            var fileName = FileName(externalSource, externalId,
                response.Content.Headers.ContentType?.MediaType);

            Directory.CreateDirectory(_storageRoot);

            var destination = Path.Combine(_storageRoot, fileName);
            var temp = destination + ".tmp";

            await using (var file = File.Create(temp))
                await response.Content.CopyToAsync(file, ct);

            File.Move(temp, destination, overwrite: true);

            return $"{UrlBase}/{fileName}";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
        {
            logger.LogWarning(ex, "Could not cache cover {ImageUrl}", imageUrl);
            return null;
        }
    }

    private static string FileName(string externalSource, string externalId, string? mediaType)
    {
        var extension = mediaType switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".jpg"
        };

        return $"{Sanitise(externalSource)}-{Sanitise(externalId)}{extension}";
    }

    private static string Sanitise(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
