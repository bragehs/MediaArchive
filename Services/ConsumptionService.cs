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
    int OpenEntryId);

public record JustClosedItem(
    int UserMediaItemId,
    string Title,
    string Creator,
    int? Rating,
    int DaysSinceClosed);

public enum MediaBucket { Gaming, Viewing, Reading }

// One home-page tile: how much of this bucket happened this week, and across how
// many distinct items. Value is hours for Gaming/Viewing, pages for Reading.
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

// Owns ConsumptionEntry and EntryNote — passes, reading and writing both.
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

                return new OpenNowItem(
                    u.Id, media.Title, media.MediaType, media.Creator ?? "",
                    media.LocalImagePath ?? media.ImageUrl, progress, daysOpen, daysSinceTouched,
                    entry?.Id ?? 0
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

    // Time/volume logged in the current calendar week (Mon–Sun, UTC to match how
    // note timestamps are stored). Effort is cumulative, so we slice each pass by
    // walking its note timeline and counting only the increments dated this week.
    public async Task<WeeklyActivity> GetWeeklyActivityAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); // back to Monday
        var weekEnd = weekStart.AddDays(6);
        var from = weekStart.ToDateTime(TimeOnly.MinValue);
        var toExclusive = weekStart.AddDays(7).ToDateTime(TimeOnly.MinValue);

        // Only passes with a note this week can contribute; load each one's full
        // note history so the cumulative-effort walk has its running baseline.
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
            // The query loads any pass with a note logged this week, but a note's
            // effort belongs to when it *happened* (ActivityDate). Skip passes with
            // no activity dated this week — e.g. a book logged now but read Feb–April
            // — so they count toward neither the units nor the item tally.
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
                default: // Movie or Show → Viewing
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

    // Effort is a running total, so a note's contribution is its cumulative value
    // minus the previous note's. Null EffortAtTime (a start note, or a comment with
    // no progress) carries the baseline forward and adds nothing.
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

    // A note's effort belongs to when it actually happened, not when it was logged.
    // A backdated Start/Finish (e.g. a book logged today but read Feb–April) belongs
    // to the pass's own StartDate/EndDate; a live Progress note has no logical date
    // of its own, so its CreatedAt stands.
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
            Text = note.Text.Trim()
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

        // Status stays my *current* relationship to the work — latest pass wins.
        userItem.Status = outcome is PassOutcome.Completed
            ? MediaStatus.Completed
            : MediaStatus.Dropped;
        userItem.Rating = finish.Rating ?? userItem.Rating;

        await db.SaveChangesAsync(ct);
    }

    // Cross-aggregate use case: import the work, then open and close a pass on it in
    // one go. Lives here because the outcome is a finished pass.
    public async Task<int> LogCompletedAsync(MediaItemDto item, WorkDetails details,
        PassStart start, PassFinish finish, CancellationToken ct = default)
    {
        var userMediaItemId = await importService.AddItemAsync(item, details, ct);
        var entryId = await StartPassAsync(userMediaItemId, start, true, ct);
        await FinishPassAsync(entryId, finish, ct);

        return userMediaItemId;
    }
}