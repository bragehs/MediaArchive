using MediaArchive.Data;
using MediaArchive.Models;
using MediaArchive.Services.Providers;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services;

public class MediaLogService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    MediaImportService importService)
{
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

    public async Task<int> LogCompletedAsync(MediaItemDto item, WorkDetails details,
        PassStart start, PassFinish finish, CancellationToken ct = default)
    {
        var userMediaItemId = await importService.AddItemAsync(item, details, ct);
        var entryId = await StartPassAsync(userMediaItemId, start, true, ct);
        await FinishPassAsync(entryId, finish, ct);

        return userMediaItemId;
    }
}