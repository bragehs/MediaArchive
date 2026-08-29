using MediaArchive.Data;
using MediaArchive.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services;

public record LibrarySearchResult(
    int UserMediaItemId, string Title, string? Creator,
    MediaType MediaType, string? ImageUrl, MediaStatus Status);

public record LibraryItem(
    int UserMediaItemId,
    string Title,
    string? Creator,
    MediaType MediaType,
    string? ImageUrl,
    int? Year,
    int? Rating,
    bool IsFavorite,
    MediaStatus Status,
    string? Universe,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Tags);

public class LibraryQueries(IDbContextFactory<AppDbContext> dbContextFactory)
{
    // The completed collection with everything the constellation renders. Creator
    // and the name lists need the graph loaded, so this materialises then projects.
    public async Task<List<LibraryItem>> GetLibraryAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var items = await CompletedWithGraph(db).ToListAsync(ct);
        return items.Select(ToLibraryItem).ToList();
    }

    // Deliberately unfiltered by status, unlike the constellation itself: the map
    // is the record of what you finished, but a dropped or half-read item still
    // has to be findable — otherwise it is reachable only through the Diary.
    public async Task<List<LibraryItem>> SearchArchiveAsync(string query,
        CancellationToken ct = default)
    {
        var q = query.Trim().ToLower();
        if (q.Length == 0) return [];

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var matches = await WithGraph(db)
            .Where(u => u.MediaItem!.Title.ToLower().Contains(q)
                        || u.MediaItem.Credits.Any(c => c.Person!.Name.ToLower().Contains(q))
                        || u.MediaItem.Genres.Any(mg => mg.Genre!.Name.ToLower().Contains(q)))
            .ToListAsync(ct);

        return matches
            .OrderBy(u => u.MediaItem!.Title.ToLower().StartsWith(q) ? 0 : 1)
            .ThenBy(u => u.MediaItem!.Title)
            .Select(ToLibraryItem)
            .ToList();
    }

    private static IQueryable<UserMediaItem> CompletedWithGraph(AppDbContext db) =>
        WithGraph(db).Where(u => u.Status == MediaStatus.Completed);

    private static IQueryable<UserMediaItem> WithGraph(AppDbContext db) =>
        db.UserMediaItems
            .Include(u => u.MediaItem).ThenInclude(m => m!.Genres).ThenInclude(mg => mg.Genre)
            .Include(u => u.MediaItem).ThenInclude(m => m!.Tags).ThenInclude(mt => mt.Tag)
            .Include(u => u.MediaItem).ThenInclude(m => m!.Credits).ThenInclude(c => c.Person)
            .Include(u => u.MediaItem).ThenInclude(m => m!.Universe)
            .AsSplitQuery()
            .AsNoTracking();

    private static LibraryItem ToLibraryItem(UserMediaItem u)
    {
        var m = u.MediaItem!;
        return new LibraryItem(
            u.Id, m.Title, m.Creator, m.MediaType,
            m.LocalImagePath ?? m.ImageUrl,
            m.ReleaseDate?.Year, u.Rating, u.IsFavorite, u.Status,
            m.Universe?.Name,
            m.Genres.Where(mg => mg.Genre is not null).Select(mg => mg.Genre!.Name).Order().ToList(),
            m.Tags.Where(mt => mt.Tag is not null).Select(mt => mt.Tag!.Name).Order().ToList());
    }

    // Credits are Included because Creator is [NotMapped] and only resolves once
    // the rows are in memory.
    public async Task<List<LibrarySearchResult>> SearchLibraryAsync(string query,
        CancellationToken ct = default)
    {
        var q = query.Trim().ToLower();
        if (q.Length == 0) return [];

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var matches = await db.UserMediaItems
            .Where(u => u.MediaItem!.Title.ToLower().Contains(q)
                        || u.MediaItem.Credits.Any(c => c.Person!.Name.ToLower().Contains(q)))
            .Include(u => u.MediaItem).ThenInclude(m => m!.Credits).ThenInclude(c => c.Person)
            .ToListAsync(ct);

        return matches
            .OrderBy(u => u.MediaItem!.Title.ToLower().StartsWith(q) ? 0 : 1)
            .ThenBy(u => u.MediaItem!.Title)
            .Take(10)
            .Select(u => new LibrarySearchResult(
                u.Id, u.MediaItem!.Title, u.MediaItem.Creator,
                u.MediaItem.MediaType,
                u.MediaItem.LocalImagePath ?? u.MediaItem.ImageUrl,
                u.Status))
            .ToList();
    }
}
