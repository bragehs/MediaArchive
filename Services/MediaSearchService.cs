using MediaArchive.Models;
using MediaArchive.Services.Providers;

namespace MediaArchive.Services;

public class MediaSearchService(IEnumerable<IMediaProvider> providers)
{
    public Task<IReadOnlyList<MediaSearchResultDto>> SearchAsync(string query, MediaType mediaType,
        CancellationToken cancellationToken = default)
    {
        return ResolveProvider(mediaType).SearchAsync(query, mediaType, cancellationToken);
    }

    public Task<MediaItemDto?> GetByIdAsync(string id, MediaType mediaType,
        CancellationToken cancellationToken = default)
    {
        return ResolveProvider(mediaType).GetByIdAsync(id, mediaType, cancellationToken);
    }

    public Task<IReadOnlyList<SeasonDto>> GetSeasonsAsync(string showExternalId,
        CancellationToken cancellationToken = default)
    {
        return ResolveProvider(MediaType.Show).GetSeasonsAsync(showExternalId, cancellationToken);
    }

    private IMediaProvider ResolveProvider(MediaType mediaType)
    {
        return providers.FirstOrDefault(p => p.CanHandle(mediaType))
               ?? throw new NotSupportedException($"No provider is registered for {mediaType}.");
    }
}