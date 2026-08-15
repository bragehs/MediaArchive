using MediaArchive.Models;

namespace MediaArchive.Services.Providers;

public interface IMediaProvider
{
    bool CanHandle(MediaType mediaType);

    Task<IReadOnlyList<MediaSearchResultDto>> SearchAsync(
        string query,
        MediaType mediaType,
        CancellationToken cancellationToken = default);

    Task<MediaItemDto?> GetByIdAsync(
        string id,
        MediaType mediaType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SeasonDto>> GetSeasonsAsync(
        string showExternalId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SeasonDto>>([]);
}