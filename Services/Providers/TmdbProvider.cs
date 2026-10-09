using System.Globalization;
using System.Net;
using System.Text.Json;
using MediaArchive.Models;

namespace MediaArchive.Services.Providers;

public class TmdbProvider(HttpClient httpClient) : IMediaProvider
{
    private const string SourceName = "Tmdb";
    private const string ImageBaseUrl = "https://image.tmdb.org/t/p/w500";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public bool CanHandle(MediaType mediaType)
    {
        return mediaType is MediaType.Movie or MediaType.Show;
    }

    public async Task<IReadOnlyList<MediaSearchResultDto>> SearchAsync(string query,
        MediaType mediaType,
        CancellationToken cancellationToken = default)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        var url = $"search/{PathSegment(mediaType)}?query={encodedQuery}&include_adult=false";

        var response =
            await httpClient.GetFromJsonAsync<TmdbResponse<TmdbSearchResult>>(url, JsonOptions, cancellationToken);

        if (response?.Results is null)
            return [];

        return response.Results
            .Select(result => MapToSearchResult(result, mediaType))
            .ToList();
    }

    public Task<MediaItemDto?> GetByIdAsync(string id,
        MediaType mediaType,
        CancellationToken cancellationToken = default)
    {
        var url = $"{PathSegment(mediaType)}/{id}?append_to_response=credits,keywords";

        return mediaType == MediaType.Movie
            ? GetItemAsync<TmdbMovieDetail>(url, MapToItem, cancellationToken)
            : GetItemAsync<TmdbTvDetail>(url, MapToItem, cancellationToken);
    }

    private static string PathSegment(MediaType mediaType)
    {
        return mediaType switch
        {
            MediaType.Movie => "movie",
            MediaType.Show => "tv",
            _ => throw new ArgumentOutOfRangeException(nameof(mediaType), mediaType, null)
        };
    }

    private async Task<MediaItemDto?> GetItemAsync<TDetail>(string url,
        Func<TDetail, MediaItemDto> map,
        CancellationToken cancellationToken) where TDetail : class
    {
        var detail = await GetOrNullAsync<TDetail>(url, cancellationToken);

        return detail is null ? null : map(detail);
    }

    private async Task<T?> GetOrNullAsync<T>(string url, CancellationToken cancellationToken) where T : class
    {
        var response = await httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    private static MediaSearchResultDto MapToSearchResult(TmdbSearchResult result, MediaType mediaType)
    {
        return new MediaSearchResultDto
        {
            ExternalSource = SourceName,
            ExternalId = result.Id.ToString(),
            MediaType = mediaType,
            Title = result.Title ?? result.Name ?? "Untitled",
            ImageUrl = PosterUrl(result.PosterPath),
            ReleaseDate = ParseDate(result.ReleaseDate ?? result.FirstAirDate)
        };
    }

    private static MediaItemDto MapToItem(TmdbMovieDetail movie)
    {
        return new MediaItemDto(
            SourceName,
            movie.Id.ToString(),
            movie.Title ?? "Untitled",
            PosterUrl(movie.PosterPath),
            ParseDate(movie.ReleaseDate),
            MediaType.Movie,
            movie.Runtime,
            movie.Overview,
            movie.Credits?.Crew?.Select(MapCrew).OfType<CreditDto>().DistinctBy(c => (c.Name, c.Role)).ToList() ?? [],
            movie.Genres?.Select(g => g.Name).ToList() ?? [],
            RatingScale.FromTen(movie.VoteAverage),
            movie.VoteCount,
            [.. movie.Keywords?.Keywords?.Select(k => k.Name).OfType<string>() ?? []]
        );
    }

    private static CreditDto? MapCrew(TmdbCrewMember crew)
    {
        return (crew.Name, crew.Job) switch
        {
            (null, _) => null,
            ({ } name, "Director") => new CreditDto(name, CreditRole.Director),
            ({ } name, "Screenplay") => new CreditDto(name, CreditRole.Screenplay),
            _ => null
        };
    }

    public async Task<IReadOnlyList<SeasonDto>> GetSeasonsAsync(string showExternalId,
        CancellationToken cancellationToken = default)
    {
        var show = await GetOrNullAsync<TmdbTvDetail>($"tv/{showExternalId}", cancellationToken);

        if (show?.Seasons is null)
            return [];

        return show.Seasons
            .Where(s => s.SeasonNumber >= 1)
            .OrderBy(s => s.SeasonNumber)
            .Select(s => new SeasonDto(
                s.SeasonNumber,
                s.Name ?? $"Season {s.SeasonNumber}",
                s.EpisodeCount,
                ParseDate(s.AirDate),
                PosterUrl(s.PosterPath)))
            .ToList();
    }

    private static MediaItemDto MapToItem(TmdbTvDetail show)
    {
        return new MediaItemDto(
            SourceName,
            show.Id.ToString(),
            show.Name ?? "Untitled",
            PosterUrl(show.PosterPath),
            ParseDate(show.FirstAirDate),
            MediaType.Show,
            show.NumberOfEpisodes,
            show.Overview,
            ShowCredits(show).ToList(),
            show.Genres?.Select(g => g.Name).ToList() ?? [],
            RatingScale.FromTen(show.VoteAverage),
            show.VoteCount,
            [.. show.Keywords?.Results?.Select(k => k.Name).OfType<string>() ?? []],
            ShowEpisodeRuntime(show)
        );
    }

    // Null over a guess: one episode's length is not the series average.
    private static int? ShowEpisodeRuntime(TmdbTvDetail show)
    {
        var runtimes = show.EpisodeRunTime?.Where(r => r > 0).ToList() ?? [];

        return runtimes.Count > 0 ? (int)Math.Round(runtimes.Average()) : null;
    }

    // Shows have no series-level director; TMDB's created_by is the headline credit.
    private static IEnumerable<CreditDto> ShowCredits(TmdbTvDetail show)
    {
        var creators = show.CreatedBy?
            .Select(c => c.Name)
            .OfType<string>()
            .Select(name => new CreditDto(name, CreditRole.Director)) ?? [];

        var networks = show.Networks?
            .Select(n => n.Name)
            .OfType<string>()
            .Select(name => new CreditDto(name, CreditRole.Studio)) ?? [];

        return creators.Concat(networks).DistinctBy(c => (c.Name, c.Role));
    }

    private static string? PosterUrl(string? posterPath)
    {
        return posterPath is not null ? $"{ImageBaseUrl}{posterPath}" : null;
    }

    private static DateOnly? ParseDate(string? date)
    {
        return DateOnly.TryParse(date, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private sealed record TmdbResponse<T>(List<T>? Results);

    // Movies carry title and release_date, shows name and first_air_date.
    private sealed record TmdbSearchResult(
        int Id,
        string? Title,
        string? Name,
        string? ReleaseDate,
        string? FirstAirDate,
        string? PosterPath);

    private sealed record TmdbMovieDetail(
        int Id,
        string? Title,
        string? Overview,
        string? ReleaseDate,
        string? PosterPath,
        int? Runtime,
        List<TmdbGenre>? Genres,
        TmdbMovieKeywords? Keywords,
        double? VoteAverage,
        int? VoteCount,
        TmdbCredits? Credits);

    private sealed record TmdbTvDetail(
        int Id,
        string? Name,
        string? Overview,
        string? FirstAirDate,
        string? PosterPath,
        int? NumberOfEpisodes,
        List<TmdbGenre>? Genres,
        List<TmdbCreatedBy>? CreatedBy,
        TmdbTvKeywords? Keywords,
        double? VoteAverage,
        int? VoteCount,
        List<TmdbCompany>? Networks,
        List<int>? EpisodeRunTime,
        List<TmdbSeason>? Seasons);

    private sealed record TmdbCreatedBy(string? Name);

    private sealed record TmdbSeason(
        int SeasonNumber,
        string? Name,
        int? EpisodeCount,
        string? AirDate,
        string? PosterPath);

    private sealed record TmdbGenre(int Id, string? Name);

    private sealed record TmdbKeyword(int Id, string? Name);

    private sealed record TmdbMovieKeywords(List<TmdbKeyword>? Keywords);

    // TV nests keywords under "results", movies under "keywords".
    private sealed record TmdbTvKeywords(List<TmdbKeyword>? Results);

    private sealed record TmdbCompany(int Id, string? Name);

    private sealed record TmdbCredits(List<TmdbCrewMember>? Crew);

    private sealed record TmdbCrewMember(string? Name, string? Job);
}