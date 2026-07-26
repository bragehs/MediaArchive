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