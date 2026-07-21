using MediaArchive.Models;

namespace MediaArchive.Services.Providers;

public interface IMediaProvider
{
    bool CanHandle(MediaType mediaType);

    Task<IReadOnlyList<MediaSearchResultDto>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<MediaItemDto?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default);
}