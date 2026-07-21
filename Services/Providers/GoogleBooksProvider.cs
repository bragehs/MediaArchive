using System.Net;
using MediaArchive.Models;
using Microsoft.Extensions.Options;

namespace MediaArchive.Services.Providers;

public class GoogleBooksProvider(HttpClient httpClient, IOptions<GoogleBooksOptions> options) : IMediaProvider
{
    private const string BaseUrl = "https://books.googleapis.com/books/v1/volumes";
    private const string SourceName = "GoogleBooks";
    private readonly string? _apiKey = options.Value.ApiKey;

    private readonly HttpClient _httpClient = httpClient;

    public bool CanHandle(MediaType mediaType)
    {
        return mediaType == MediaType.Book;
    }

    public async Task<MediaItemDto?> GetByIdAsync(string id,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/{Uri.EscapeDataString(id)}";

        if (!string.IsNullOrWhiteSpace(_apiKey))
            url += $"?key={_apiKey}";

        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var volume = await response.Content
            .ReadFromJsonAsync<GoogleBooksVolume>(cancellationToken);

        return volume is null ? null : MapToItem(volume);
    }

    public async Task<IReadOnlyList<MediaSearchResultDto>> SearchAsync(string query,
        CancellationToken cancellationToken = default)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        var url = $"{BaseUrl}?q=intitle:{encodedQuery}&maxResults=5";

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

    private static MediaItemDto MapToItem(GoogleBooksVolume volume)
    {
        var info = volume.VolumeInfo;
        var authors = info?.Authors ?? [];

        return new MediaItemDto(
            SourceName,
            volume.Id,
            info?.Title ?? "Untitled",
            info?.ImageLinks?.Thumbnail,
            ParseYear(info?.PublishedDate),
            MediaType.Book,
            info?.PageCount,
            info?.Description,
            authors.Count > 0 ? string.Join(", ", authors) : null,
            info?.Categories ?? [],
            RatingScale.FromFive(info?.AverageRating),
            info?.RatingsCount);
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
        string? PublishedDate,
        string? Description,
        int? PageCount,
        List<string>? Categories,
        double? AverageRating,
        int? RatingsCount,
        GoogleBooksImageLinks? ImageLinks);

    private sealed record GoogleBooksImageLinks(string? Thumbnail);
}