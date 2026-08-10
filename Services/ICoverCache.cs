namespace MediaArchive.Services;

// The seam MediaImportService depends on. The head picks the implementation:
// NullCoverCache (render provider URLs) or CoverCacheService (cache to disk).
public interface ICoverCache
{
    Task<string?> TryCacheAsync(string? imageUrl, string? externalSource,
        string? externalId, CancellationToken ct = default);
}
