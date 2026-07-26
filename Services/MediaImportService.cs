using MediaArchive.Data;
using MediaArchive.Models;
using MediaArchive.Services.Providers;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services;

// The controlled vocabulary as it currently exists, for pick-or-create inputs.
public record Vocabulary(
    List<string> Genres,
    List<string> Tags,
    List<string> Universes,
    List<string> Series);

public class MediaImportService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    CoverCacheService coverCache)
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

        if (mediaItem.LocalImagePath is null)
            mediaItem.LocalImagePath = await coverCache.TryCacheAsync(
                mediaItem.ImageUrl, mediaItem.ExternalSource, mediaItem.ExternalId, ct);

        // Additive: re-importing something must not drop vocabulary I added by hand.
        await VocabularyResolver.ApplyWorkDetailsAsync(db, mediaItem, details, false, ct);
        await VocabularyResolver.ApplyCreditsAsync(db, mediaItem, item.Credits, ct);

        var userItem = await ResolveUserMediaItemAsync(db, mediaItem, ct);
        userItem.Discovery = details.Discovery ?? userItem.Discovery;

        await db.SaveChangesAsync(ct);

        return userItem.Id;
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
