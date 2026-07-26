using MediaArchive.Data;
using MediaArchive.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services;

public record OnDeckItem(int UserMediaItemId, string Title, string? ImageUrl);

public record OpenPassSummary(int EntryId, DateOnly? StartDate, int? Effort, double? Progress);

public record ItemDetail(
    int UserMediaItemId,
    int MediaItemId,
    string Title,
    MediaType MediaType,
    string? Creator,
    string? ImageUrl,
    string? Description,
    DateOnly? ReleaseDate,
    int? Length,
    double? ExternalRating,
    int? ExternalRatingCount,
    string? Universe,
    string? Series,
    int? SeriesPosition,
    List<string> Genres,
    List<TagInput> Tags,
    MediaStatus Status,
    int? Rating,
    bool IsFavorite,
    DiscoverySource? Discovery,
    DateOnly AddedDate,
    OpenPassSummary? OpenPass,
    int PassCount);

public class CollectionService(IDbContextFactory<AppDbContext> dbContextFactory)
{
    public async Task<List<OnDeckItem>> GetOnDeckAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        return await db.UserMediaItems
            .Where(u => u.Status == MediaStatus.Interested)
            .OrderBy(u => u.AddedDate)
            .Select(u => new OnDeckItem(u.Id, u.MediaItem!.Title,
                u.MediaItem.LocalImagePath ?? u.MediaItem.ImageUrl))
            .ToListAsync(ct);
    }

    public async Task<ItemDetail?> GetItemDetailAsync(int userMediaItemId,
        CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        
        var item = await db.UserMediaItems
            .Where(u => u.Id == userMediaItemId)
            .Include(u => u.MediaItem).ThenInclude(m => m!.Genres).ThenInclude(mg => mg.Genre)
            .Include(u => u.MediaItem).ThenInclude(m => m!.Tags).ThenInclude(mt => mt.Tag)
            .Include(u => u.MediaItem).ThenInclude(m => m!.Credits).ThenInclude(mc => mc.Person)
            .Include(u => u.MediaItem).ThenInclude(m => m!.Series)
            .Include(u => u.MediaItem).ThenInclude(m => m!.Universe)
            .Include(u => u.Entries)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (item is null)
            return null;

        var media = item.MediaItem!;
        var open = item.Entries.FirstOrDefault(e => e.EndDate is null);

        var openPass = open is null
            ? null
            : new OpenPassSummary(open.Id, open.StartDate, open.Effort,
                open.Effort is { } effort && media.Length is { } length
                    ? (double)effort / length * 100
                    : null);

        return new ItemDetail(
            item.Id,
            media.Id,
            media.Title,
            media.MediaType,
            media.Creator,
            media.LocalImagePath ?? media.ImageUrl,
            media.Description,
            media.ReleaseDate,
            media.Length,
            media.ExternalRating,
            media.ExternalRatingCount,
            media.Universe?.Name,
            media.Series is not null && media.Series.Id != Series.StandaloneId
                ? media.Series.Name
                : null,
            media.SeriesPosition,
            media.Genres.Where(mg => mg.Genre is not null)
                .Select(mg => mg.Genre!.Name).Order().ToList(),
            media.Tags.Where(mt => mt.Tag is not null)
                .Select(mt => new TagInput(mt.Tag!.Name, mt.Tag.Facet, mt.Tag.AppliesTo))
                .OrderBy(t => t.Name)
                .ToList(),
            item.Status,
            item.Rating,
            item.IsFavorite,
            item.Discovery,
            item.AddedDate,
            openPass,
            item.Entries.Count);
    }

    // Genres, tags, series, universe — replace semantics: the form is the truth.
    public async Task UpdateDetailsAsync(int mediaItemId, WorkDetails details,
        CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var mediaItem = await db.MediaItems.FirstAsync(m => m.Id == mediaItemId, ct);

        await VocabularyResolver.ApplyWorkDetailsAsync(db, mediaItem, details, true, ct);

        if (details.Discovery is { } discovery)
        {
            var userItem = await db.UserMediaItems
                .FirstOrDefaultAsync(u => u.MediaItemId == mediaItemId, ct);

            if (userItem is not null)
                userItem.Discovery = discovery;
        }

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