namespace MediaArchive.Services;

// Phase-1 mobile: skip local caching so the UI renders provider image URLs
// directly (needs network). Swapped for CoverCacheService once the WebView can
// serve runtime-written files.
public sealed class NullCoverCache : ICoverCache
{
    public Task<string?> TryCacheAsync(string? imageUrl, string? externalSource,
        string? externalId, CancellationToken ct = default)
        => Task.FromResult<string?>(null);
}
