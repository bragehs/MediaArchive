using MediaArchive.Models;

namespace MediaArchive.Services.Providers;

public interface IMediaProvider
{
    bool CanHandle(MediaType mediaType);

    Task<IReadOnlyList<MediaSearchResultDto>> SearchAsync(
        string query,
        MediaType mediaType,
        CancellationToken cancellationToken = default);
}