using MediaArchive.Models;
using Microsoft.Extensions.Options;

namespace MediaArchive.Services.Providers;

public class IgdbProvider(HttpClient httpClient, IOptions<IgdbOptions> options) : IMediaProvider
{
    private const string BaseUrl = "https://api.igdb.com/v4";
    private const string SourceName = "Igdb";

    private readonly HttpClient _httpClient = httpClient;
    private readonly IgdbOptions _options = options.Value;

    public bool CanHandle(MediaType mediaType)
    {
        return mediaType == MediaType.Game;
    }

    public Task<IReadOnlyList<MediaSearchResultDto>> SearchAsync(string query,
        MediaType _,
        CancellationToken cancellationToken = default)
    {
        // IGDB uses POST /games with an Apicalypse query body where YOU list the fields:
        //   search "query"; fields name,first_release_date,involved_companies; limit 5;
        // Auth needs a Twitch bearer token built from ClientId/ClientSecret (cache it).
        // Map each game to the skinny MediaSearchResultDto.
        throw new NotImplementedException();
    }

    // Unlike TMDB, IGDB never *needs* a second endpoint — a single POST /games can select
    // every field at once (cover, summary, involved_companies, etc). So GetDetailAsync is
    // really just "query /games with a wider fields list, filtered by id":
    //   fields name,summary,cover.url,first_release_date,involved_companies.company.name;
    //   where id = {externalId};
    // Map into the rich MediaItemDto. Genres/Universe stay null — filled manually.
    // (If a search already returned everything you need, this can defer to that instead.)
    public Task<MediaItemDto?> GetByIdAsync(string id,
        MediaType _,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}