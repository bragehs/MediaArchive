using MediaArchive.Data;
using MediaArchive.Models;
using MediaArchive.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services.Queries;

// One row of the home-screen widget's snapshot. Serialized to JSON as-is, so
// the property names are part of the contract with the Swift side.
public record WidgetRow(
    int Id,             // UserMediaItemId — what /item/{id} routes on
    string Title,
    string Kind,
    string ProgressLabel,
    double? Percent,
    string? Cover);     // bare cover filename; null when only a remote URL exists

public class WidgetQueries(IDbContextFactory<AppDbContext> dbContextFactory)
{
    // Same "open now" definition as HomeQueries.GetOpenNowAsync, but ordered by
    // recency: the widget drops the least recently touched items when full.
    public async Task<List<WidgetRow>> GetInProgressAsync(int take = 8, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var items = await db.UserMediaItems
            .Where(u => u.Status == MediaStatus.InProgress)
            .Include(u => u.MediaItem)
            .Include(u => u.Entries.Where(e => e.EndDate == null)).ThenInclude(e => e.Notes)
            .AsNoTracking()
            .ToListAsync(ct);

        return items
            .Select(u =>
            {
                var media = u.MediaItem!;
                var entry = u.Entries.SingleOrDefault();

                var lastTouched = entry is not null && entry.Notes.Count > 0
                    ? entry.Notes.Max(n => n.CreatedAt)
                    : (entry?.StartDate ?? u.AddedDate).ToDateTime(TimeOnly.MinValue);

                var row = new WidgetRow(
                    u.Id, media.Title, UiHelpers.TypeLabel(media.MediaType),
                    entry?.Effort is { } effort
                        ? $"{effort} {UiHelpers.LengthUnit(media.MediaType)}"
                        : "just started",
                    EffortMath.ProgressPercent(entry?.Effort, media.Length),
                    CoverFileName(media.LocalImagePath));

                return (Row: row, LastTouched: lastTouched);
            })
            .OrderByDescending(x => x.LastTouched)
            .Take(take)
            .Select(x => x.Row)
            .ToList();
    }

    // LocalImagePath stores a WebView pseudo-URL (covers://c/<file>); the widget
    // wants the filename it can resolve inside the shared container.
    private static string? CoverFileName(string? localImagePath) =>
        localImagePath is not null && localImagePath.StartsWith(CoverCacheService.UrlBase + "/")
            ? localImagePath[(CoverCacheService.UrlBase.Length + 1)..]
            : null;
}
