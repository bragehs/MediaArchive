using System.Net;
using System.Text.RegularExpressions;
using MediaArchive.Models;
using Microsoft.Extensions.Options;

namespace MediaArchive.Services.Providers;

public partial class GoogleBooksProvider(HttpClient httpClient, IOptions<GoogleBooksOptions> options) : IMediaProvider
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
        MediaType _,
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
        MediaType _,
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
            NormaliseCoverUrl(info?.ImageLinks?.Thumbnail),
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
            NormaliseCoverUrl(info?.ImageLinks?.Thumbnail),
            ParseYear(info?.PublishedDate),
            MediaType.Book,
            info?.PageCount,
            CleanDescription(info?.Description),
            authors.Count > 0 ? string.Join(", ", authors) : null,
            info?.Categories ?? [],
            RatingScale.FromFive(info?.AverageRating),
            info?.RatingsCount);
    }

    // Google Books serves covers over http, which the browser blocks as mixed content
    // once the app is on https. zoom=1 is a 128px thumbnail; zoom=2 is usable at the
    // size the log panel draws it. edge=curl paints a fake page-curl over the art.
    private static string? NormaliseCoverUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        return url
            .Replace("http://", "https://", StringComparison.OrdinalIgnoreCase)
            .Replace("&edge=curl", "", StringComparison.OrdinalIgnoreCase)
            .Replace("zoom=1", "zoom=2", StringComparison.OrdinalIgnoreCase);
    }

    private static string? CleanDescription(string? htmlDescription)
    {
        if (string.IsNullOrWhiteSpace(htmlDescription))
            return null;

        var text = BreakTags().Replace(htmlDescription, "\n");
        text = AnyTag().Replace(text, "");
        text = WebUtility.HtmlDecode(text);

        // &nbsp; decodes to a non-breaking space, which no Trim overload catches.
        text = text.Replace(' ', ' ');
        text = HorizontalSpace().Replace(text, " ");
        text = BlankLines().Replace(text, "\n\n");

        text = text.Trim();

        return text.Length == 0 ? null : text;
    }

    [GeneratedRegex(@"<\s*br\s*/?\s*>|<\s*/\s*(p|div|li|h[1-6])\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakTags();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"[^\S\n]+")]
    private static partial Regex HorizontalSpace();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();

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