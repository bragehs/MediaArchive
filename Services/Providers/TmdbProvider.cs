using System.Globalization;
using System.Net;
using System.Text.Json;
using MediaArchive.Models;

namespace MediaArchive.Services.Providers;

public class TmdbProvider(HttpClient httpClient) : IMediaProvider
{
    private const string SourceName = "Tmdb";
    private const string ImageBaseUrl = "https://image.tmdb.org/t/p/w500";

    // TMDB is snake_case; the Web defaults alone would leave those properties null.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _httpClient = httpClient;

    public bool CanHandle(MediaType mediaType)
    {
        return mediaType is MediaType.Movie or MediaType.Show;
    }

    public Task<IReadOnlyList<MediaSearchResultDto>> SearchAsync(string query,
        MediaType mediaType,
        CancellationToken cancellationToken = default)
    {
        return mediaType switch
        {
            MediaType.Movie => SearchMoviesAsync(query, cancellationToken),
            MediaType.Show => SearchShowsAsync(query, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(mediaType), mediaType, null)
        };
    }

    public Task<MediaItemDto?> GetByIdAsync(string id,
        MediaType mediaType,
        CancellationToken cancellationToken = default)
    {
        return mediaType switch
        {
            MediaType.Movie => GetByIdMoviesAsync(id, cancellationToken),
            MediaType.Show => GetByIdShowsAsync(id, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(mediaType), mediaType, null)
        };
    }

    public async Task<MediaItemDto?> GetByIdMoviesAsync(string id, CancellationToken cancellationToken = default)
    {
        var url = $"movie/{id}?append_to_response=credits,keywords";

        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var volume = await response.Content
            .ReadFromJsonAsync<TmdbMovieDetail>(JsonOptions, cancellationToken);

        return volume is null ? null : MapToItem(volume);
    }

    public async Task<IReadOnlyList<MediaSearchResultDto>> SearchMoviesAsync(string query,
        CancellationToken cancellationToken = default)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        var url = $"search/movie?query={encodedQuery}&include_adult=false";

        var response =
            await _httpClient.GetFromJsonAsync<TmdbResponse<TmdbMovieResult>>(url, JsonOptions, cancellationToken);

        if (response?.Results is null)
            return [];

        return response.Results
            .Select(MapToSearchResult)
            .ToList();
    }

    private static MediaItemDto MapToItem(TmdbMovieDetail movie)
    {
        return new MediaItemDto(
            SourceName,
            movie.Id.ToString(),
            movie.Title ?? "Untitled",
            movie.PosterPath is not null ? $"{ImageBaseUrl}{movie.PosterPath}" : null,
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


    private static MediaSearchResultDto MapToSearchResult(TmdbMovieResult movie)
    {
        return new MediaSearchResultDto(
            SourceName,
            movie.Id.ToString(),
            MediaType.Movie,
            movie.Title ?? "Untitled",
            movie.PosterPath is not null ? $"{ImageBaseUrl}{movie.PosterPath}" : null,
            ParseDate(movie.ReleaseDate)
        );
    }

    public async Task<IReadOnlyList<MediaSearchResultDto>> SearchShowsAsync(string query,
        CancellationToken cancellationToken = default)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        var url = $"search/tv?query={encodedQuery}&include_adult=false";

        var response =
            await _httpClient.GetFromJsonAsync<TmdbResponse<TmdbTvResult>>(url, JsonOptions, cancellationToken);

        if (response?.Results is null)
            return [];

        return response.Results
            .Select(MapToSearchResult)
            .ToList();
    }

    private static MediaSearchResultDto MapToSearchResult(TmdbTvResult show)
    {
        return new MediaSearchResultDto(
            SourceName,
            show.Id.ToString(),
            MediaType.Show,
            show.Name ?? "Untitled",
            show.PosterPath is not null ? $"{ImageBaseUrl}{show.PosterPath}" : null,
            ParseDate(show.FirstAirDate)
        );
    }

    public async Task<MediaItemDto?> GetByIdShowsAsync(string id, CancellationToken cancellationToken = default)
    {
        var url = $"tv/{id}?append_to_response=credits,keywords";

        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var show = await response.Content
            .ReadFromJsonAsync<TmdbTvDetail>(JsonOptions, cancellationToken);

        return show is null ? null : MapToItem(show);
    }

    // Option A: each season is captured as its own item. The seasons array comes
    // back on the base tv/{id} call — no append needed.
    public async Task<IReadOnlyList<SeasonDto>> GetSeasonsAsync(string showExternalId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"tv/{showExternalId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();

        var show = await response.Content
            .ReadFromJsonAsync<TmdbTvDetail>(JsonOptions, cancellationToken);

        if (show?.Seasons is null)
            return [];

        // Skip "Specials" (season 0); present real seasons in order.
        return show.Seasons
            .Where(s => s.SeasonNumber >= 1)
            .OrderBy(s => s.SeasonNumber)
            .Select(s => new SeasonDto(
                s.SeasonNumber,
                s.Name ?? $"Season {s.SeasonNumber}",
                s.EpisodeCount,
                ParseDate(s.AirDate),
                s.PosterPath is not null ? $"{ImageBaseUrl}{s.PosterPath}" : null))
            .ToList();
    }

    private static MediaItemDto MapToItem(TmdbTvDetail show)
    {
        return new MediaItemDto(
            SourceName,
            show.Id.ToString(),
            show.Name ?? "Untitled",
            show.PosterPath is not null ? $"{ImageBaseUrl}{show.PosterPath}" : null,
            ParseDate(show.FirstAirDate),
            MediaType.Show,
            show.NumberOfEpisodes,
            show.Overview,
            ShowCredits(show).ToList(),
            show.Genres?.Select(g => g.Name).ToList() ?? [],
            RatingScale.FromTen(show.VoteAverage),
            show.VoteCount,
            [.. show.Keywords?.Results?.Select(k => k.Name).OfType<string>() ?? []],
            // Series-level runtime is empty on many newer shows; fall back to the latest episode.
            show.EpisodeRunTime?.FirstOrDefault(r => r > 0) ?? show.LastEpisodeToAir?.Runtime
        );
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

    // Full ISO date, but "" for unreleased titles.
    private static DateOnly? ParseDate(string? date)
    {
        return DateOnly.TryParse(date, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private sealed record TmdbResponse<T>(List<T>? Results);

    private sealed record TmdbMovieResult(
        int Id,
        string? Title,
        string? ReleaseDate,
        string? PosterPath);

    private sealed record TmdbTvResult(
        int Id,
        string? Name,
        string? FirstAirDate,
        string? PosterPath);

    // Detail: fetched with append_to_response so credits arrive in the same call.
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
        // Often empty on newer shows — fall back to LastEpisodeToAir.Runtime.
        List<int>? EpisodeRunTime,
        TmdbEpisode? LastEpisodeToAir,
        List<TmdbSeason>? Seasons);

    private sealed record TmdbCreatedBy(string? Name);

    private sealed record TmdbEpisode(int? Runtime);

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