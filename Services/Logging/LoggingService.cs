using MediaArchive.Data;
using MediaArchive.Models;
using MediaArchive.Services.Import;
using MediaArchive.Services.Providers;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services.Logging;

public class LoggingService(
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

        var entry = OpenPass(userItem, start);

        await db.SaveChangesAsync(ct);

        return entry.Id;
    }

    // A new interval starting at the dropped pass's effort — the source pass
    // keeps its verdict and the dormant gap stays visible.
    public async Task<int> ResumePassAsync(int entryId, PassStart start,
        CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var source = await db.ConsumptionEntries
            .Include(e => e.UserMediaItem!).ThenInclude(u => u.Entries)
            .FirstAsync(e => e.Id == entryId, ct);

        if (source.EndDate is null)
            throw new InvalidOperationException($"Pass {entryId} is still open.");

        var userItem = source.UserMediaItem!;

        if (userItem.Entries.Any(e => e.EndDate is null))
            throw new InvalidOperationException(
                $"UserMediaItem {userItem.Id} already has an open pass.");

        var entry = OpenPass(userItem, start, source);

        await db.SaveChangesAsync(ct);

        return entry.Id;
    }

    private static ConsumptionEntry OpenPass(UserMediaItem userItem, PassStart start,
        ConsumptionEntry? resumes = null)
    {
        var entry = new ConsumptionEntry
        {
            StartDate = start.StartDate ?? DateOnly.FromDateTime(DateTime.Today),
            Context = start.Context ?? resumes?.Context,
            ResumesEntryId = resumes?.Id,
            StartingEffort = resumes?.Effort,
            Effort = resumes?.Effort
        };

        if (!string.IsNullOrWhiteSpace(start.Note))
            entry.Notes.Add(new EntryNote { Kind = NoteKind.Start, Text = start.Note.Trim() });

        userItem.Entries.Add(entry);
        userItem.Status = MediaStatus.InProgress;

        return entry;
    }

    // With a session, the sitting is closed and linked to the new note in the same save.
    public async Task AddNoteAsync(int entryId, NoteInput note, SessionEnd? session = null,
        CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var entry = await db.ConsumptionEntries
            .Include(e => e.Notes)
            .FirstAsync(e => e.Id == entryId, ct);

        if (entry.EndDate is not null)
            throw new InvalidOperationException($"Pass {entryId} is already finished.");

        if (note.EffortAtTime is not null)
            entry.Effort = note.EffortAtTime;

        var progress = new EntryNote
        {
            Kind = NoteKind.Progress,
            EffortAtTime = entry.Effort,
            Text = string.IsNullOrWhiteSpace(note.Text) ? null : note.Text.Trim()
        };
        entry.Notes.Add(progress);

        if (session is not null)
            await CloseSessionAsync(db, entryId, session, progress, ct);

        await db.SaveChangesAsync(ct);
    }

    public async Task FinishPassAsync(int entryId, PassFinish finish, SessionEnd? session = null,
        CancellationToken ct = default)
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

        EntryNote? finishNote = null;
        if (!string.IsNullOrWhiteSpace(finish.Note))
        {
            finishNote = new EntryNote
            {
                Kind = NoteKind.Finish,
                EffortAtTime = entry.Effort,
                Text = finish.Note.Trim()
            };
            entry.Notes.Add(finishNote);
        }

        if (session is not null)
            await CloseSessionAsync(db, entryId, session, finishNote, ct);

        userItem.Status = outcome is PassOutcome.Completed
            ? MediaStatus.Completed
            : MediaStatus.Dropped;
        userItem.Rating = finish.Rating ?? userItem.Rating;

        await db.SaveChangesAsync(ct);
    }

    // One sitting at a time, anywhere: refused here so no screen can request a second activity.
    public async Task<int> StartSessionAsync(int entryId, DateTime? startedAt = null,
        CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var entry = await db.ConsumptionEntries.FirstAsync(e => e.Id == entryId, ct);

        if (entry.EndDate is not null)
            throw new InvalidOperationException($"Pass {entryId} is already finished.");

        if (await db.Sessions.AnyAsync(s => s.EndedAt == null, ct))
            throw new InvalidOperationException("A session is already running.");

        var session = new Session { StartedAt = startedAt ?? DateTime.UtcNow };
        entry.Sessions.Add(session);

        await db.SaveChangesAsync(ct);

        return session.Id;
    }

    // The sitting produced nothing to log: the sheet was dismissed, or a stale one discarded.
    public async Task EndSessionAsync(SessionEnd end, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        await CloseSessionAsync(db, null, end, null, ct);

        await db.SaveChangesAsync(ct);
    }

    private static async Task CloseSessionAsync(AppDbContext db, int? entryId, SessionEnd end,
        EntryNote? note, CancellationToken ct)
    {
        var session = await db.Sessions.FirstAsync(s => s.Id == end.SessionId, ct);

        if (session.EndedAt is not null)
            throw new InvalidOperationException($"Session {end.SessionId} has already ended.");
        if (entryId is { } id && session.ConsumptionEntryId != id)
            throw new InvalidOperationException($"Session {end.SessionId} belongs to another pass.");

        session.EndedAt = end.EndedAt;
        session.PausedMinutes = Math.Max(0, end.PausedMinutes);
        session.EntryNote = note;
    }

    public async Task<int> LogCompletedAsync(MediaItemDto item, WorkDetails details,
        PassStart start, PassFinish finish, CancellationToken ct = default)
    {
        var userMediaItemId = await importService.AddItemAsync(item, details, ct);
        var entryId = await StartPassAsync(userMediaItemId, start, true, ct);
        await FinishPassAsync(entryId, finish, ct: ct);

        return userMediaItemId;
    }
}
