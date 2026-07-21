using MediaArchive.Models;
using Microsoft.Extensions.Options;

namespace MediaArchive.Services.Providers;

public class TmdbProvider(HttpClient httpClient, IOptions<TmdbOptions> options) : IMediaProvider
{
    private const string BaseUrl = "https://api.themoviedb.org/3";
    private const string SourceName = "Tmdb";
    private readonly string? _apiKey = options.Value.ApiKey;

    private readonly HttpClient _httpClient = httpClient;

    public bool CanHandle(MediaType mediaType)
    {
        return mediaType is MediaType.Movie or MediaType.Show;
    }

    public Task<IReadOnlyList<MediaSearchResultDto>> SearchAsync(string query,
        CancellationToken cancellationToken = default)
    {
        // GET /search/movie (or /search/tv) ?query=...
        // Returns title, overview, poster_path, release_date — enough for the skinny
        // MediaSearchResultDto. Map each result to a search DTO.
        throw new NotImplementedException();
    }

    // This is why the two-DTO split exists. TMDB's search endpoint does NOT return
    // runtime, credits (cast/crew), keywords, or belongs_to_collection. Those live only
    // on the detail endpoint:
    //   GET /movie/{id}?append_to_response=credits,keywords
    // One call with append_to_response bundles the extras, so no third request needed.
    // Map the response into the rich MediaItemDto: runtime -> Length, overview ->
    // Description, poster_path -> ImageUrl, and the director from credits -> Creator.
    // Genres/Universe stay null here — they're filled manually in the add form.
    public Task<MediaItemDto?> GetByIdAsync(string id,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
