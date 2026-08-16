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
    int OpenEntryId,
    bool IsAudiobook,
    double? AudioHours,
    int? PageCount);

public record JustClosedItem(
    int UserMediaItemId,
    string Title,
    string Creator,
    int? Rating,
    int DaysSinceClosed);

public enum MediaBucket { Gaming, Viewing, Reading }

public record WeeklyBucketStat(MediaBucket Bucket, double Value, string Unit, int ItemsTouched);

public record WeeklyActivity(DateOnly WeekStart, DateOnly WeekEnd,
    IReadOnlyList<WeeklyBucketStat> Buckets);

public class HomeQueries(
    IDbContextFactory<AppDbContext> dbContextFactory)
{
    public async Task<List<OpenNowItem>> GetOpenNowAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var items = await db.UserMediaItems
            .Where(u => u.Status == MediaStatus.InProgress)
            .Include(u => u.MediaItem).ThenInclude(m => m!.Credits).ThenInclude(c => c.Person)
            .Include(u => u.Entries.Where(e => e.EndDate == null)).ThenInclude(e => e.Notes)
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.Today);

        return items.Select(u =>
            {
                var media = u.MediaItem!;
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

                var book = media as Book;
                var isAudiobook = book is not null && entry?.Context == ConsumptionContext.Audiobook;

                return new OpenNowItem(
                    u.Id, media.Title, media.MediaType, media.Creator ?? "",
                    media.LocalImagePath ?? media.ImageUrl, progress, daysOpen, daysSinceTouched,
                    entry?.Id ?? 0,
                    isAudiobook, book?.AudioHours, book?.PageCount
                );
            })
            .OrderByDescending(x => x.Progress)
            .ToList();
    }

    public async Task<JustClosedItem?> GetJustClosedAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var entry = await db.ConsumptionEntries
            .Where(e => e.EndDate != null && e.UserMediaItem!.Status == MediaStatus.Completed)
            .Include(e => e.UserMediaItem).ThenInclude(u => u!.MediaItem)
            .ThenInclude(m => m!.Credits).ThenInclude(c => c.Person)
            .OrderByDescending(e => e.EndDate)
            .FirstOrDefaultAsync(ct);

        if (entry is null) return null;

        var media = entry.UserMediaItem!.MediaItem!;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var daysSinceClosed = today.DayNumber - entry.EndDate!.Value.DayNumber;

        return new JustClosedItem(
            entry.UserMediaItem.Id, media.Title, media.Creator ?? "",
            entry.UserMediaItem.Rating, daysSinceClosed);
    }

    public async Task<WeeklyActivity> GetWeeklyActivityAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var weekEnd = weekStart.AddDays(6);
        var from = weekStart.ToDateTime(TimeOnly.MinValue);
        var toExclusive = weekStart.AddDays(7).ToDateTime(TimeOnly.MinValue);

        // Load each pass's full note history so the cumulative-effort walk has its
        // running baseline — filtering the Include to this week would silently
        // corrupt the running total.
        var entries = await db.ConsumptionEntries
            .Where(e => e.Notes.Any(n => n.CreatedAt >= from && n.CreatedAt < toExclusive))
            .Include(e => e.Notes)
            .Include(e => e.UserMediaItem!).ThenInclude(u => u.MediaItem)
            .AsNoTracking()
            .ToListAsync(ct);

        double gamingMinutes = 0, viewingMinutes = 0, readingPages = 0;
        var gamingItems = new HashSet<int>();
        var viewingItems = new HashSet<int>();
        var readingItems = new HashSet<int>();

        foreach (var entry in entries)
        {
            var touchedThisWeek = entry.Notes.Any(n =>
            {
                var when = ActivityDate(entry, n);
                return when >= from && when < toExclusive;
            });
            if (!touchedThisWeek)
                continue;

            var media = entry.UserMediaItem!.MediaItem!;
            var units = UnitsLoggedInWeek(entry, from, toExclusive);
            var minutes = media.MinutesPerUnit is { } perUnit ? units * perUnit : 0;

            switch (media.MediaType)
            {
                case MediaType.Game:
                    gamingMinutes += minutes;
                    gamingItems.Add(entry.UserMediaItemId);
                    break;
                case MediaType.Book:
                    readingPages += units;
                    readingItems.Add(entry.UserMediaItemId);
                    break;
                default:
                    viewingMinutes += minutes;
                    viewingItems.Add(entry.UserMediaItemId);
                    break;
            }
        }

        var buckets = new List<WeeklyBucketStat>
        {
            new(MediaBucket.Gaming, Math.Round(gamingMinutes / 60, 1), "h", gamingItems.Count),
            new(MediaBucket.Viewing, Math.Round(viewingMinutes / 60, 1), "h", viewingItems.Count),
            new(MediaBucket.Reading, Math.Round(readingPages), "pages", readingItems.Count)
        };

        return new WeeklyActivity(weekStart, weekEnd, buckets);
    }

    private static double UnitsLoggedInWeek(ConsumptionEntry entry, DateTime from, DateTime toExclusive)
    {
        double previous = 0, units = 0;
        foreach (var note in entry.Notes.OrderBy(n => n.CreatedAt))
        {
            double cumulative = note.EffortAtTime ?? previous;
            var increment = Math.Max(0, cumulative - previous);
            var when = ActivityDate(entry, note);
            if (when >= from && when < toExclusive)
                units += increment;
            previous = cumulative;
        }
        return units;
    }

    private static DateTime ActivityDate(ConsumptionEntry entry, EntryNote note) => note.Kind switch
    {
        NoteKind.Finish => (entry.EndDate ?? DateOnly.FromDateTime(note.CreatedAt)).ToDateTime(TimeOnly.MinValue),
        NoteKind.Start => (entry.StartDate ?? DateOnly.FromDateTime(note.CreatedAt)).ToDateTime(TimeOnly.MinValue),
        _ => note.CreatedAt
    };
}