using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MediaArchive.Models;

namespace MediaArchive.Services.Providers;

public class IgdbProvider(HttpClient httpClient, IgdbAuthenticator authenticator) : IMediaProvider
{
    private const string SourceName = "Igdb";
    private const string ImageBaseUrl = "https://images.igdb.com/igdb/image/upload";

    private const int SearchLimit = 5;

    // IGDB is snake_case; the Web defaults alone would leave those properties null.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _httpClient = httpClient;
    private readonly IgdbAuthenticator _authenticator = authenticator;

    public bool CanHandle(MediaType mediaType)
    {
        return mediaType == MediaType.Game;
    }

    // IGDB uses POST /games with an Apicalypse body where the caller lists the fields it wants.
    public async Task<IReadOnlyList<MediaSearchResultDto>> SearchAsync(string query,
        MediaType _,
        CancellationToken cancellationToken = default)
    {
        var body = $"search \"{Escape(query)}\"; "
                 + "fields name,first_release_date,cover.image_id; "
                 + $"limit {SearchLimit};";

        var games = await QueryGamesAsync(body, cancellationToken);

        return games.Select(MapToSearchResult).ToList();
    }

    // Unlike TMDB, one POST /games selects every field at once, so no second endpoint is needed.
    public async Task<MediaItemDto?> GetByIdAsync(string id,
        MediaType _,
        CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(id, out var gameId))
            return null;

        var body = "fields name,summary,first_release_date,cover.image_id,"
                 + "involved_companies.company.name,involved_companies.developer,"
                 + "genres.name,themes.name,keywords.name,total_rating,total_rating_count; "
                 + $"where id = {gameId};";

        var games = await QueryGamesAsync(body, cancellationToken);

        var game = games.FirstOrDefault();

        return game is null ? null : MapToItem(game);
    }

    private async Task<List<IgdbGame>> QueryGamesAsync(string body, CancellationToken cancellationToken)
    {
        var token = await _authenticator.GetTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "games")
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<List<IgdbGame>>(JsonOptions, cancellationToken) ?? [];
    }

    private static MediaSearchResultDto MapToSearchResult(IgdbGame game)
    {
        return new MediaSearchResultDto(
            SourceName,
            game.Id.ToString(),
            MediaType.Game,
            game.Name ?? "Untitled",
            CoverUrl(game.Cover?.ImageId, "t_cover_big"),
            ParseDate(game.FirstReleaseDate));
    }

    private static MediaItemDto MapToItem(IgdbGame game)
    {
        return new MediaItemDto(
            SourceName,
            game.Id.ToString(),
            game.Name ?? "Untitled",
            CoverUrl(game.Cover?.ImageId, "t_cover_big"),
            ParseDate(game.FirstReleaseDate),
            MediaType.Game,
            null, // Games have no runtime/length.
            game.Summary,
            Studios(game).ToList(),
            // Per the mapping choice: IGDB themes are our genres, IGDB genres become tags.
            game.Themes?.Select(t => t.Name).ToList() ?? [],
            RatingScale.FromHundred(game.TotalRating),
            game.TotalRatingCount,
            [.. Tags(game)]);
    }

    // Only the companies flagged as developers become the headline Studio credit.
    private static IEnumerable<CreditDto> Studios(IgdbGame game)
    {
        return game.InvolvedCompanies?
            .Where(c => c.Developer && c.Company?.Name is not null)
            .Select(c => new CreditDto(c.Company!.Name!, CreditRole.Studio))
            .DistinctBy(c => c.Name) ?? [];
    }

    private static IEnumerable<string> Tags(IgdbGame game)
    {
        var genres = game.Genres?.Select(g => g.Name) ?? [];
        var keywords = game.Keywords?.Select(k => k.Name) ?? [];

        return genres.Concat(keywords)
            .OfType<string>()
            .DistinctBy(t => t.ToLowerInvariant());
    }

    private static string? CoverUrl(string? imageId, string size)
    {
        return imageId is null ? null : $"{ImageBaseUrl}/{size}/{imageId}.jpg";
    }

    // first_release_date is a Unix timestamp in seconds.
    private static DateOnly? ParseDate(long? unixSeconds)
    {
        return unixSeconds is > 0
            ? DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(unixSeconds.Value).UtcDateTime)
            : null;
    }

    // Apicalypse wraps the search term in double quotes, so escape any the query carries.
    private static string Escape(string query)
    {
        return query.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private sealed record IgdbGame(
        long Id,
        string? Name,
        string? Summary,
        long? FirstReleaseDate,
        IgdbCover? Cover,
        List<IgdbInvolvedCompany>? InvolvedCompanies,
        List<IgdbNamed>? Genres,
        List<IgdbNamed>? Themes,
        List<IgdbNamed>? Keywords,
        double? TotalRating,
        int? TotalRatingCount);

    private sealed record IgdbCover(string? ImageId);

    private sealed record IgdbInvolvedCompany(IgdbCompany? Company, bool Developer, bool Publisher);

    private sealed record IgdbCompany(string? Name);

    private sealed record IgdbNamed(string? Name);
}
