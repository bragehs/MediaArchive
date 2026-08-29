namespace MediaArchive.Services.Infrastructure;

public interface ICoverCache
{
    Task<string?> TryCacheAsync(string? imageUrl, string? externalSource,
        string? externalId, CancellationToken ct = default);
}
