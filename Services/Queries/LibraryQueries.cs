using MediaArchive.Data;
using MediaArchive.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services;

public record LibrarySearchResult(
    int UserMediaItemId, string Title, string? Creator,
    MediaType MediaType, string? ImageUrl, MediaStatus Status);

public class LibraryQueries(IDbContextFactory<AppDbContext> dbContextFactory)
{
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
