using MediaArchive.Data;
using MediaArchive.Models;
using MediaArchive.Services.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MediaArchive.Services;

// The controlled vocabulary as it currently exists, for pick-or-create inputs.
public record Vocabulary(
    List<string> Genres,
    List<string> Tags,
    List<string> Universes,
    List<string> Series);

public class MediaImportService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICoverCache coverCache,
    ILogger<MediaImportService> logger)
{
    public async Task<Vocabulary> GetVocabularyAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        // Single-user archive: these lists stay small enough to load whole.
        var genres = await db.Genres.OrderBy(g => g.Name).Select(g => g.Name).ToListAsync(ct);
        var tags = await db.Tags.OrderBy(t => t.Name).Select(t => t.Name).ToListAsync(ct);
        var universes = await db.Universes.OrderBy(u => u.Name).Select(u => u.Name).ToListAsync(ct);
        var series = await db.Series.OrderBy(s => s.Name).Select(s => s.Name).ToListAsync(ct);

        return new Vocabulary(genres, tags, universes, series);
    }

    public async Task<int> AddItemAsync(MediaItemDto item, WorkDetails details,
        CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var mediaItem = await ResolveMediaItemAsync(db, item, ct);

        // Audiobook length is user-entered, not from the provider — set it when given.
        if (mediaItem is Book book && details.AudioHours is { } audioHours)
            book.AudioHours = audioHours;

        // Additive: re-importing something must not drop vocabulary I added by hand.
        await VocabularyResolver.ApplyWorkDetailsAsync(db, mediaItem, details, false, ct);
        await VocabularyResolver.ApplyCreditsAsync(db, mediaItem, item.Credits, ct);

        var userItem = await ResolveUserMediaItemAsync(db, mediaItem, ct);
        userItem.Discovery = details.Discovery ?? userItem.Discovery;

        await db.SaveChangesAsync(ct);

        // Cover download is slow (OpenLibrary redirects through archive.org), so keep
        // it off the add path: the item is saved now and the cover fills in once
        // fetched. mediaItem.Id is populated by SaveChangesAsync above.
        if (mediaItem.LocalImagePath is null && !string.IsNullOrWhiteSpace(mediaItem.ImageUrl))
            QueueCoverCache(mediaItem.Id, mediaItem.ImageUrl,
                mediaItem.ExternalSource, mediaItem.ExternalId);

        return userItem.Id;
    }

    // Covers for items added before caching worked (or whose download failed) stay
    // null forever otherwise. Queue a background fetch for each on launch.
    public async Task BackfillUncachedCoversAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var uncached = await db.MediaItems
            .Where(m => m.LocalImagePath == null && m.ImageUrl != null)
            .Select(m => new { m.Id, m.ImageUrl, m.ExternalSource, m.ExternalId })
            .ToListAsync(ct);

        foreach (var m in uncached)
            QueueCoverCache(m.Id, m.ImageUrl!, m.ExternalSource, m.ExternalId);
    }

    // Fire-and-forget: download the cover on a background task and write the local
    // path back through a fresh DbContext (the request-scoped one is disposed by then).
    private void QueueCoverCache(int mediaItemId, string imageUrl,
        string? externalSource, string? externalId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var localPath = await coverCache.TryCacheAsync(imageUrl, externalSource, externalId);
                if (localPath is null)
                    return;

                await using var db = await dbContextFactory.CreateDbContextAsync();
                var item = await db.MediaItems.FirstOrDefaultAsync(m => m.Id == mediaItemId);
                if (item is null)
                    return;

                item.LocalImagePath = localPath;
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Background cover cache failed for media item {MediaItemId}",
                    mediaItemId);
            }
        });
    }

    private static async Task<MediaItem> ResolveMediaItemAsync(AppDbContext db, MediaItemDto dto,
        CancellationToken ct)
    {
        var existing = await db.MediaItems
            .FirstOrDefaultAsync(
                m => m.ExternalSource == dto.ExternalSource && m.ExternalId == dto.ExternalId, ct);

        if (existing is not null)
            return existing;

        var created = MediaItemMapper.ToEntity(dto);

        db.MediaItems.Add(created);
        return created;
    }

    private static async Task<UserMediaItem> ResolveUserMediaItemAsync(AppDbContext db,
        MediaItem mediaItem, CancellationToken ct)
    {
        if (mediaItem.Id != 0)
        {
            var existing = await db.UserMediaItems
                .FirstOrDefaultAsync(u => u.MediaItemId == mediaItem.Id, ct);

            if (existing is not null)
                return existing;
        }

        var created = new UserMediaItem { MediaItem = mediaItem };
        db.UserMediaItems.Add(created);
        return created;
    }
}
