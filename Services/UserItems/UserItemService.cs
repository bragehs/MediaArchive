using MediaArchive.Data;
using MediaArchive.Models;
using MediaArchive.Services.Import;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services.UserItems;

public class UserItemService(IDbContextFactory<AppDbContext> dbContextFactory)
{
    public async Task UpdateDetailsAsync(int userMediaItemId, WorkDetails details,
        CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var userItem = await db.UserMediaItems
            .Include(u => u.MediaItem)
            .FirstAsync(u => u.Id == userMediaItemId, ct);

        await VocabularyResolver.ApplyWorkDetailsAsync(db, userItem.MediaItem!, details,
            replace: true, ct);

        if (details.Discovery is { } discovery)
            userItem.Discovery = discovery;

        await db.SaveChangesAsync(ct);
    }

    public Task SetRatingAsync(int userMediaItemId, int? rating, CancellationToken ct = default)
    {
        return UpdateUserItemAsync(userMediaItemId, u => u.Rating = rating, ct);
    }

    public Task SetFavoriteAsync(int userMediaItemId, bool isFavorite,
        CancellationToken ct = default)
    {
        return UpdateUserItemAsync(userMediaItemId, u => u.IsFavorite = isFavorite, ct);
    }

    private async Task UpdateUserItemAsync(int userMediaItemId, Action<UserMediaItem> change,
        CancellationToken ct)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var userItem = await db.UserMediaItems.FirstAsync(u => u.Id == userMediaItemId, ct);
        change(userItem);

        await db.SaveChangesAsync(ct);
    }
}
