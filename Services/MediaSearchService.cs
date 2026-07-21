using MediaArchive.Models;
using MediaArchive.Services.Providers;

namespace MediaArchive.Services;

public class MediaSearchService(IEnumerable<IMediaProvider> providers)
{
    public Task<IReadOnlyList<MediaSearchResultDto>> SearchAsync(string query, MediaType mediaType,
        CancellationToken cancellationToken = default)
    {
        return ResolveProvider(mediaType).SearchAsync(query, cancellationToken);
    }

    public Task<MediaItemDto?> GetByIdAsync(string id, MediaType mediaType,
        CancellationToken cancellationToken = default)
    {
        return ResolveProvider(mediaType).GetByIdAsync(id, cancellationToken);
    }

    private IMediaProvider ResolveProvider(MediaType mediaType)
    {
        return providers.FirstOrDefault(p => p.CanHandle(mediaType))
               ?? throw new NotSupportedException($"No provider is registered for {mediaType}.");
    }
}
