using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MediaArchive.Models;

namespace MediaArchive.Services.Providers;

public partial class OpenLibraryProvider(HttpClient httpClient) : IMediaProvider
{
    public const string UserAgent =
        "MediaArchive (https://github.com/bragehs/MediaArchive; brage.skjorestad@gmail.com)";

    private const string BaseUrl = "https://openlibrary.org";
    private const string CoverBaseUrl = "https://covers.openlibrary.org/b/id";
    private const string SourceName = "OpenLibrary";

    private const string SearchFields =
        "key,title,author_name,first_publish_year,cover_i,number_of_pages_median,ratings_average,ratings_count,subject";

    private const int SearchLimit = 5;
    private const int MaxGenres = 8;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public bool CanHandle(MediaType mediaType)
    {
        return mediaType == MediaType.Book;
    }

    public async Task<MediaItemDto?> GetByIdAsync(string id,
        MediaType _,
        CancellationToken cancellationToken = default)
    {
        var workId = NormaliseWorkId(id);

        var docTask = GetSearchDocAsync(workId, cancellationToken);
        var workTask = GetWorkAsync(workId, cancellationToken);

        await Task.WhenAll(docTask, workTask);

        var doc = await docTask;
        var work = await workTask;

        if (doc is null && work is null)
            return null;

        return MapToItem(workId, doc, work);
    }

    public async Task<IReadOnlyList<MediaSearchResultDto>> SearchAsync(string query,
        MediaType _,
        CancellationToken cancellationToken = default)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        var url = $"{BaseUrl}/search.json?q={encodedQuery}&fields={SearchFields}&limit={SearchLimit}";

        var response = await httpClient
            .GetFromJsonAsync<OpenLibrarySearchResponse>(url, JsonOptions, cancellationToken);

        if (response?.Docs is null)
            return [];

        return response.Docs
            .Select(MapToSearchResult)
            .ToList();
    }

    private async Task<OpenLibraryDoc?> GetSearchDocAsync(string workId, CancellationToken cancellationToken)
    {
        var encodedQuery = Uri.EscapeDataString($"key:/works/{workId}");
        var url = $"{BaseUrl}/search.json?q={encodedQuery}&fields={SearchFields}&limit=1";

        var response = await httpClient
            .GetFromJsonAsync<OpenLibrarySearchResponse>(url, JsonOptions, cancellationToken);

        return response?.Docs?.FirstOrDefault();
    }

    private async Task<OpenLibraryWork?> GetWorkAsync(string workId, CancellationToken cancellationToken)
    {
        var url = $"{BaseUrl}/works/{Uri.EscapeDataString(workId)}.json";

        var response = await httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<OpenLibraryWork>(JsonOptions, cancellationToken);
    }

    private static MediaSearchResultDto MapToSearchResult(OpenLibraryDoc doc)
    {
        return new MediaSearchResultDto(
            SourceName,
            NormaliseWorkId(doc.Key),
            MediaType.Book,
            doc.Title ?? "Untitled",
            CoverUrl(doc.CoverI, 'M'),
            ParseYear(doc.FirstPublishYear));
    }

    private static MediaItemDto MapToItem(string workId, OpenLibraryDoc? doc, OpenLibraryWork? work)
    {
        var authors = doc?.AuthorName ?? [];

        return new MediaItemDto(
            SourceName,
            workId,
            doc?.Title ?? work?.Title ?? "Untitled",
            CoverUrl(doc?.CoverI ?? work?.Covers?.FirstOrDefault(c => c > 0), 'L'),
            ParseYear(doc?.FirstPublishYear),
            MediaType.Book,
            doc?.NumberOfPagesMedian,
            CleanDescription(work?.Description),
            [.. authors.Select(a => new CreditDto(a, CreditRole.Author))],
            [.. CleanSubjects(doc?.Subject ?? work?.Subjects)],
            RatingScale.FromFive(doc?.RatingsAverage),
            doc?.RatingsCount,
            []);
    }

    private static string NormaliseWorkId(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "";

        var lastSlash = key.LastIndexOf('/');

        return lastSlash >= 0 ? key[(lastSlash + 1)..] : key;
    }

    private static string? CoverUrl(int? coverId, char size)
    {
        return coverId is > 0 ? $"{CoverBaseUrl}/{coverId}-{size}.jpg" : null;
    }

    // The subject feed mixes real topics with machine tags like "award:hugo_award=1966".
    private static IEnumerable<string> CleanSubjects(List<string>? subjects)
    {
        if (subjects is null)
            return [];

        return subjects
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Where(s => !s.Contains(':') && !s.Contains('='))
            .DistinctBy(s => s.ToLowerInvariant())
            .Take(MaxGenres);
    }

    private static string? CleanDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        var text = description.Replace("\r\n", "\n").Replace('\r', '\n');

        var footer = SourceFooter().Match(text);
        if (footer.Success)
            text = text[..footer.Index];

        text = MarkdownLink().Replace(text, "$1");
        text = ReferenceLink().Replace(text, "$1");

        text = BreakTags().Replace(text, "\n");
        text = AnyTag().Replace(text, "");
        text = WebUtility.HtmlDecode(text);

        // &nbsp; decodes to a non-breaking space, which no Trim overload catches.
        text = text.Replace('\u00A0', ' ');
        text = HorizontalSpace().Replace(text, " ");
        text = BlankLines().Replace(text, "\n\n");

        text = text.Trim();

        return text.Length == 0 ? null : text;
    }

    [GeneratedRegex(@"\n\s*-{3,}\s*\n")]
    private static partial Regex SourceFooter();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex MarkdownLink();

    [GeneratedRegex(@"\[([^\]]*)\]\[[^\]]*\]")]
    private static partial Regex ReferenceLink();

    [GeneratedRegex(@"<\s*br\s*/?\s*>|<\s*/\s*(p|div|li|h[1-6])\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakTags();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"[^\S\n]+")]
    private static partial Regex HorizontalSpace();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();

    private static DateOnly? ParseYear(int? year)
    {
        return year is > 0 and < 3000 ? new DateOnly(year.Value, 1, 1) : null;
    }

    private sealed record OpenLibrarySearchResponse(List<OpenLibraryDoc>? Docs);

    private sealed record OpenLibraryDoc(
        string? Key,
        string? Title,
        List<string>? AuthorName,
        int? FirstPublishYear,
        int? CoverI,
        int? NumberOfPagesMedian,
        double? RatingsAverage,
        int? RatingsCount,
        List<string>? Subject);

    private sealed record OpenLibraryWork(
        string? Title,
        List<int>? Covers,
        List<string>? Subjects,
        [property: JsonConverter(typeof(OpenLibraryTextConverter))]
        string? Description);

    private sealed class OpenLibraryTextConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
                return reader.GetString();

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                return null;
            }

            using var document = JsonDocument.ParseValue(ref reader);

            return document.RootElement.TryGetProperty("value", out var value)
                ? value.GetString()
                : null;
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}