using MediaArchive.Data;
using MediaArchive.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services;

public record OpenNowItem(
    int UserMediaItemId,
    string Title,
    MediaType MediaType,
    string Creator,
    string? ImageUrl,
    double? Progress,
    int DaysOpen,
    int DaysSinceTouched,
    int OpenEntryId);

public record JustClosedItem(
    int UserMediaItemId,
    string Title,
    string Creator,
    int? Rating,
    int DaysSinceClosed);

public class ConsumptionService(IDbContextFactory<AppDbContext> dbContextFactory)
{
    public async Task<List<OpenNowItem>> GetOpenNowAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var items = await db.UserMediaItems
            .Where(u => u.Status == MediaStatus.InProgress)
            .Include(u => u.MediaItem).ThenInclude(m => m.Credits).ThenInclude(c => c.Person)
            .Include(u => u.Entries.Where(e => e.EndDate == null)).ThenInclude(e => e.Notes)
            .ToListAsync(ct);
        ;

        var today = DateOnly.FromDateTime(DateTime.Today);

        return items.Select(u =>
            {
                var media = u.MediaItem;
                var entry = u.Entries.SingleOrDefault();
                double? progress = entry?.Effort is { } effort && media.Length is { } length
                    ? (double)effort / length * 100
                    : null;
                var startDate = entry?.StartDate ?? today;
                var daysOpen = today.DayNumber - startDate.DayNumber;

                var lastTouched = entry is not null && entry.Notes.Count > 0
                    ? DateOnly.FromDateTime(entry.Notes.Max(n => n.CreatedAt))
                    : startDate;
                var daysSinceTouched = today.DayNumber - lastTouched.DayNumber;

                return new OpenNowItem(
                    u.Id, media.Title, media.MediaType, media.Creator,
                    media.ImageUrl, progress, daysOpen, daysSinceTouched,
                    entry?.Id ?? 0
                );
            })
            .OrderByDescending(x => x.Progress)
            .ToList();
    }

    public async Task<JustClosedItem> GetJustClosedAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var entry = await db.ConsumptionEntries
            .Where(e => e.EndDate != null && e.UserMediaItem.Status == MediaStatus.Completed)
            .Include(e => e.UserMediaItem).ThenInclude(u => u.MediaItem)
            .ThenInclude(m => m.Credits).ThenInclude(c => c.Person)
            .OrderByDescending(e => e.EndDate)
            .FirstOrDefaultAsync(ct);

        if (entry is null) return null;

        var media = entry.UserMediaItem.MediaItem;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var daysSinceClosed = today.DayNumber - entry.EndDate.Value.DayNumber;

        return new JustClosedItem(
            entry.UserMediaItem.Id, media.Title, media.Creator, entry.UserMediaItem.Rating, daysSinceClosed);
    }
}