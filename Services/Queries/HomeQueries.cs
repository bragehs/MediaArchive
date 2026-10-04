using MediaArchive.Data;
using MediaArchive.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services.Queries;

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

public enum MediaBucket { Gaming, Viewing, Reading }

public record WeeklyBucketStat(MediaBucket Bucket, double Value, string Unit, int ItemsTouched);

// Effort per bucket from logs; days touched and time sat from logs and sittings.
public record WeeklyActivity(DateOnly WeekStart, DateOnly WeekEnd,
    IReadOnlyList<WeeklyBucketStat> Buckets,
    IReadOnlyList<DateOnly> ActiveDays, int Sittings, int MinutesSat);

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

        var today = EffortMath.Today;

        return items.Select(u =>
            {
                var media = u.MediaItem!;
                var entry = u.Entries.SingleOrDefault();
                var progress = EffortMath.ProgressPercent(entry?.Effort, media.Length);
                var startDate = entry?.StartDate ?? today;
                var daysOpen = today.DayNumber - startDate.DayNumber;

                var lastTouched = entry is not null && entry.Notes.Count > 0
                    ? EffortMath.LocalDay(entry.Notes.Max(n => n.CreatedAt))
                    : startDate;
                var daysSinceTouched = today.DayNumber - lastTouched.DayNumber;

                return new OpenNowItem(
                    u.Id, media.Title, media.MediaType, media.Creator ?? "",
                    media.DisplayImageUrl, progress, daysOpen, daysSinceTouched,
                    entry?.Id ?? 0);
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
        var today = EffortMath.Today;
        var daysSinceClosed = today.DayNumber - entry.EndDate!.Value.DayNumber;

        return new JustClosedItem(
            entry.UserMediaItem.Id, media.Title, media.Creator ?? "",
            entry.UserMediaItem.Rating, daysSinceClosed);
    }

    public async Task<WeeklyActivity> GetWeeklyActivityAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var today = EffortMath.Today;
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var weekEnd = weekStart.AddDays(6);
        var from = weekStart.ToDateTime(TimeOnly.MinValue);
        var toExclusive = weekStart.AddDays(7).ToDateTime(TimeOnly.MinValue);
        var fromUtc = from.AddHours(EffortMath.DayStartsAt).ToUniversalTime();
        var toUtc = toExclusive.AddHours(EffortMath.DayStartsAt).ToUniversalTime();

        // The effort walk needs each pass's full note history as its baseline —
        // filtering the Include to this week would corrupt the deltas.
        var entries = await db.ConsumptionEntries
            .Where(e => e.Notes.Any(n => n.CreatedAt >= fromUtc && n.CreatedAt < toUtc))
            .Include(e => e.Notes)
            .Include(e => e.UserMediaItem!).ThenInclude(u => u.MediaItem)
            .AsNoTracking()
            .ToListAsync(ct);

        var sittings = await db.Sessions
            .Where(s => s.EndedAt != null && s.StartedAt >= fromUtc && s.StartedAt < toUtc)
            .AsNoTracking()
            .ToListAsync(ct);

        double gamingMinutes = 0, viewingMinutes = 0, readingPages = 0;
        var gamingItems = new HashSet<int>();
        var viewingItems = new HashSet<int>();
        var readingItems = new HashSet<int>();
        var activeDays = sittings.Select(s => EffortMath.LocalDay(s.StartedAt)).ToHashSet();

        foreach (var entry in entries)
        {
            var daysTouched = entry.Notes
                .Select(n => EffortMath.ActivityDate(entry, n))
                .Where(when => when >= from && when < toExclusive)
                .Select(DateOnly.FromDateTime)
                .ToList();
            if (daysTouched.Count == 0)
                continue;
            activeDays.UnionWith(daysTouched);

            var media = entry.UserMediaItem!.MediaItem!;
            var units = EffortMath.UnitsLogged(entry, from, toExclusive);
            var minutes = EffortMath.ToMinutes(media, units) ?? 0;

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

        return new WeeklyActivity(weekStart, weekEnd, buckets,
            activeDays.Order().ToList(), sittings.Count, sittings.Sum(s => s.Minutes ?? 0));
    }

}
