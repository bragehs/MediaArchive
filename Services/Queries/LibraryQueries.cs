using MediaArchive.Data;
using MediaArchive.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services.Queries;

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
    DateOnly AddedDate,
    DateOnly? LastActivity,
    string? Universe,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Tags);

public class LibraryQueries(IDbContextFactory<AppDbContext> dbContextFactory)
{
    public async Task<List<LibraryItem>> GetLibraryAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var items = await ArchiveWithGraph(db).ToListAsync(ct);
        return items.Select(ToLibraryItem).ToList();
    }

    // Unfiltered by status: dropped and in-progress items must stay findable.
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

    // What you have been through: Interested is Explore's backlog, InProgress is Home's.
    private static IQueryable<UserMediaItem> ArchiveWithGraph(AppDbContext db) =>
        WithGraph(db).Where(u => u.Status == MediaStatus.Completed || u.Status == MediaStatus.Dropped);

    private static IQueryable<UserMediaItem> WithGraph(AppDbContext db) =>
        db.UserMediaItems
            .Include(u => u.MediaItem).ThenInclude(m => m!.Genres).ThenInclude(mg => mg.Genre)
            .Include(u => u.MediaItem).ThenInclude(m => m!.Tags).ThenInclude(mt => mt.Tag)
            .Include(u => u.MediaItem).ThenInclude(m => m!.Credits).ThenInclude(c => c.Person)
            .Include(u => u.MediaItem).ThenInclude(m => m!.Universe)
            .Include(u => u.Entries)
            .AsSplitQuery()
            .AsNoTracking();

    private static LibraryItem ToLibraryItem(UserMediaItem u)
    {
        var m = u.MediaItem!;
        return new LibraryItem(
            u.Id, m.Title, m.Creator, m.MediaType,
            m.DisplayImageUrl,
            m.ReleaseDate?.Year, u.Rating, u.IsFavorite, u.Status,
            u.AddedDate, u.Entries.Select(e => e.EndDate ?? e.StartDate).Max(),
            m.Universe?.Name,
            m.Genres.Where(mg => mg.Genre is not null).Select(mg => mg.Genre!.Name).Order().ToList(),
            m.Tags.Where(mt => mt.Tag is not null).Select(mt => mt.Tag!.Name).Order().ToList());
    }

}
