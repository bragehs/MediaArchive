using MediaArchive.Data;
using MediaArchive.Models;
using MediaArchive.Services.Providers;
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

public record PassNote(DateTime CreatedAt, NoteKind Kind, int? EffortAtTime, string? Text);

public record PassSummary(
    int EntryId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    PassOutcome? Outcome,
    int? RatingAtTime,
    int? Effort,
    ConsumptionContext? Context,
    List<PassNote> Notes);

public class ConsumptionService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    MediaImportService importService)
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

    public async Task<List<PassSummary>> GetPassHistoryAsync(int userMediaItemId,
        CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var entries = await db.ConsumptionEntries
            .Where(e => e.UserMediaItemId == userMediaItemId)
            .Include(e => e.Notes)
            .OrderByDescending(e => e.StartDate)
            .ThenByDescending(e => e.Id)
            .AsNoTracking()
            .ToListAsync(ct);

        return entries
            .Select(e => new PassSummary(
                e.Id, e.StartDate, e.EndDate, e.Outcome, e.RatingAtTime, e.Effort, e.Context,
                e.Notes
                    .OrderBy(n => n.CreatedAt)
                    .Select(n => new PassNote(n.CreatedAt, n.Kind, n.EffortAtTime, n.Text))
                    .ToList()))
            .ToList();
    }

    public async Task<int> StartPassAsync(int userMediaItemId, PassStart start,
        bool allowConcurrent = false, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var userItem = await db.UserMediaItems
            .Include(u => u.Entries)
            .FirstAsync(u => u.Id == userMediaItemId, ct);

        if (!allowConcurrent && userItem.Entries.Any(e => e.EndDate is null))
            throw new InvalidOperationException(
                $"UserMediaItem {userMediaItemId} already has an open pass.");

        var entry = new ConsumptionEntry
        {
            StartDate = start.StartDate ?? DateOnly.FromDateTime(DateTime.Today),
            Context = start.Context
        };

        if (!string.IsNullOrWhiteSpace(start.Note))
            entry.Notes.Add(new EntryNote { Kind = NoteKind.Start, Text = start.Note.Trim() });

        userItem.Entries.Add(entry);
        userItem.Status = MediaStatus.InProgress;

        await db.SaveChangesAsync(ct);

        return entry.Id;
    }

    public async Task AddNoteAsync(int entryId, NoteInput note, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var entry = await db.ConsumptionEntries
            .Include(e => e.Notes)
            .FirstAsync(e => e.Id == entryId, ct);

        if (entry.EndDate is not null)
            throw new InvalidOperationException($"Pass {entryId} is already finished.");

        if (note.EffortAtTime is not null)
            entry.Effort = note.EffortAtTime;

        entry.Notes.Add(new EntryNote
        {
            Kind = NoteKind.Progress,
            EffortAtTime = entry.Effort,
            Text = string.IsNullOrWhiteSpace(note.Text) ? null : note.Text.Trim()
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task FinishPassAsync(int entryId, PassFinish finish, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var entry = await db.ConsumptionEntries
            .Include(e => e.Notes)
            .Include(e => e.UserMediaItem!).ThenInclude(u => u.MediaItem)
            .FirstAsync(e => e.Id == entryId, ct);

        var userItem = entry.UserMediaItem!;
        var outcome = finish.Dropped ? PassOutcome.Dropped : PassOutcome.Completed;

        entry.Effort = finish.Effort ?? entry.Effort;

        entry.EndDate = finish.EndDate;
        entry.Outcome = outcome;
        entry.RatingAtTime = finish.Rating;

        if (!string.IsNullOrWhiteSpace(finish.Note))
            entry.Notes.Add(new EntryNote
            {
                Kind = NoteKind.Finish,
                EffortAtTime = entry.Effort,
                Text = finish.Note.Trim()
            });

        userItem.Status = outcome is PassOutcome.Completed
            ? MediaStatus.Completed
            : MediaStatus.Dropped;
        userItem.Rating = finish.Rating ?? userItem.Rating;

        await db.SaveChangesAsync(ct);
    }

    public async Task<int> LogCompletedAsync(MediaItemDto item, WorkDetails details,
        PassStart start, PassFinish finish, CancellationToken ct = default)
    {
        var userMediaItemId = await importService.AddItemAsync(item, details, ct);
        var entryId = await StartPassAsync(userMediaItemId, start, true, ct);
        await FinishPassAsync(entryId, finish, ct);

        return userMediaItemId;
    }
}