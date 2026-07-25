using MediaArchive.Data;
using MediaArchive.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services;

public record OnDeckItem(int UserMediaItemId, string Title, string? ImageUrl);

public class CollectionService(IDbContextFactory<AppDbContext> dbContextFactory)
{
    // Interested items, oldest-queued first. Pure SQL projection — Title and cover
    // are plain columns, so no in-memory step is needed.
    public async Task<List<OnDeckItem>> GetOnDeckAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        return await db.UserMediaItems
            .Where(u => u.Status == MediaStatus.Interested)
            .OrderBy(u => u.AddedDate)
            .Select(u => new OnDeckItem(u.Id, u.MediaItem!.Title, u.MediaItem.ImageUrl))
            .ToListAsync(ct);
    }
}
