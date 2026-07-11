using System.Net.Http.Json;
using MediaArchive.Models;
using Microsoft.Extensions.Options;

namespace MediaArchive.Services.Providers;

public class GoogleBooksProvider(HttpClient httpClient, IOptions<GoogleBooksOptions> options) : IMediaProvider
{
    private const string BaseUrl = "https://www.googleapis.com/books/v1/volumes";
    private const string SourceName = "GoogleBooks";

    private readonly HttpClient _httpClient = httpClient;
    private readonly string? _apiKey = options.Value.ApiKey;

    public bool CanHandle(MediaType mediaType)
    {
        return mediaType == MediaType.Book;
    }

    public async Task<IReadOnlyList<MediaSearchResultDto>> SearchAsync(string query, MediaType mediaType,
        CancellationToken cancellationToken = default)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        var url = $"{BaseUrl}?q={encodedQuery}&maxResults=20";

        // The key is optional; only append it when one is configured.
        if (!string.IsNullOrWhiteSpace(_apiKey))
            url += $"&key={_apiKey}";

        var response = await _httpClient.GetFromJsonAsync<GoogleBooksResponse>(url, cancellationToken);

        if (response?.Items is null)
            return [];

        return response.Items
            .Select(MapToSearchResult)
            .ToList();
    }

    private static MediaSearchResultDto MapToSearchResult(GoogleBooksVolume volume)
    {
        var info = volume.VolumeInfo;

        return new MediaSearchResultDto(
            SourceName,
            volume.Id,
            MediaType.Book,
            info?.Title ?? "Untitled",
            info?.Authors ?? [],
            ParseYear(info?.PublishedDate));
    }

    private static int? ParseYear(string? publishedDate)
    {
        if (publishedDate is null || publishedDate.Length < 4)
            return null;

        return int.TryParse(publishedDate[..4], out var year) ? year : null;
    }

    private sealed record GoogleBooksResponse(List<GoogleBooksVolume>? Items);

    private sealed record GoogleBooksVolume(string Id, GoogleBooksVolumeInfo? VolumeInfo);

    private sealed record GoogleBooksVolumeInfo(
        string? Title,
        List<string>? Authors,
        string? PublishedDate);
}