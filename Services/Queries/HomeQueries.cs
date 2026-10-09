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

public static class MediaBuckets
{
    public static MediaBucket Of(MediaType type) => type switch
    {
        MediaType.Game => MediaBucket.Gaming,
        MediaType.Book => MediaBucket.Reading,
        _ => MediaBucket.Viewing
    };

    public static double Amount(MediaItem media, double units) =>
        Of(media.MediaType) == MediaBucket.Reading ? units : EffortMath.ToMinutes(media, units) ?? 0;

    public static double Rounded(MediaBucket bucket, double amount) =>
        bucket == MediaBucket.Reading ? Math.Round(amount) : Math.Round(amount / 60, 1);
}

public record WeeklyBucketStat(MediaBucket Bucket, double Value, string Unit, int ItemsTouched);

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

        var week = Week.Containing(EffortMath.Today);
        var (fromUtc, toUtc) = (week.FromUtc, week.ToUtc);

        // Unfiltered Include: the effort walk needs each pass's full history or the deltas are wrong.
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

        var touched = entries
            .Select(e => (Entry: e, Days: DaysTouched(e, week)))
            .Where(t => t.Days.Count > 0)
            .ToList();
        var activeDays = sittings
            .Select(s => EffortMath.LocalDay(s.StartedAt))
            .Concat(touched.SelectMany(t => t.Days))
            .Distinct()
            .Order()
            .ToList();
        var logs = touched.Select(t => WeekLog.Of(t.Entry, week)).ToList();

        var buckets = new List<WeeklyBucketStat>
        {
            BucketStat(logs, MediaBucket.Gaming, "h"),
            BucketStat(logs, MediaBucket.Viewing, "h"),
            BucketStat(logs, MediaBucket.Reading, "pages")
        };

        return new WeeklyActivity(week.Start, week.End, buckets,
            activeDays, sittings.Count, sittings.Sum(s => s.Minutes ?? 0));
    }

    private static List<DateOnly> DaysTouched(ConsumptionEntry entry, Week week) => entry.Notes
        .Select(n => EffortMath.ActivityDate(entry, n))
        .Where(week.Contains)
        .Select(DateOnly.FromDateTime)
        .ToList();

    private static WeeklyBucketStat BucketStat(List<WeekLog> logs, MediaBucket bucket, string unit)
    {
        var inBucket = logs.Where(l => l.Bucket == bucket).ToList();
        return new WeeklyBucketStat(bucket, MediaBuckets.Rounded(bucket, inBucket.Sum(l => l.Amount)), unit,
            inBucket.Select(l => l.UserMediaItemId).Distinct().Count());
    }

    private sealed record WeekLog(MediaBucket Bucket, int UserMediaItemId, double Amount)
    {
        public static WeekLog Of(ConsumptionEntry entry, Week week)
        {
            var media = entry.UserMediaItem!.MediaItem!;
            var units = EffortMath.UnitsLogged(entry, week.From, week.ToExclusive);
            return new WeekLog(MediaBuckets.Of(media.MediaType), entry.UserMediaItemId, MediaBuckets.Amount(media, units));
        }
    }

    private sealed record Week(DateOnly Start)
    {
        public DateOnly End => Start.AddDays(6);
        public DateTime From => Start.ToDateTime(TimeOnly.MinValue);
        public DateTime ToExclusive => Start.AddDays(7).ToDateTime(TimeOnly.MinValue);
        public DateTime FromUtc => From.AddHours(EffortMath.DayStartsAt).ToUniversalTime();
        public DateTime ToUtc => ToExclusive.AddHours(EffortMath.DayStartsAt).ToUniversalTime();

        public bool Contains(DateTime when) => when >= From && when < ToExclusive;

        public static Week Containing(DateOnly day) => new(day.AddDays(-(((int)day.DayOfWeek + 6) % 7)));
    }
}
