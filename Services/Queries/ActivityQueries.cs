using System.Globalization;
using MediaArchive.Data;
using MediaArchive.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services.Queries;

// Sat is a session that resolved into no note; one that did lends its minutes to that note's event.
public enum ActivityKind { Started, Resumed, Progress, Finished, Dropped, Sat }

public record ActivityEvent(
    int UserMediaItemId,
    string Title,
    MediaType MediaType,
    string? ImageUrl,
    ActivityKind Kind,
    DateOnly Date,
    string? Note,
    int? Rating,
    ConsumptionContext? Context,
    double? EffortDelta,
    double? EffortAtTime,
    int? Length,
    bool IsReread,
    int? Minutes)
{
    public bool IsMilestone => Kind is not (ActivityKind.Progress or ActivityKind.Sat);
    public bool IsSilent => !IsMilestone && string.IsNullOrWhiteSpace(Note);
}

// One item's silent logs and sessions on one day, folded into one line.
public record ActivityRun(
    int UserMediaItemId,
    string Title,
    MediaType MediaType,
    string? ImageUrl,
    int Logs,
    double EffortDelta,
    int MinutesSat);

// The calendar cell is decided here: the loudest event's cover and kind. The
// cover is `ImageUrl` because that is the one name the bridge resolves to a file.
public record ActivityDay(
    DateOnly Date,
    string? ImageUrl,
    string Title,
    ActivityKind Loudest,
    int Items,
    int MinutesSat,
    IReadOnlyList<ActivityEvent> Events,
    IReadOnlyList<ActivityRun> Runs);

public record ActivityMonth(int Year, int Month, string Name,
    int Logs, int Finished, int MinutesSat, IReadOnlyList<ActivityDay> Days);

public record ActivityCalendar(IReadOnlyList<ActivityMonth> Months);

public class ActivityQueries(IDbContextFactory<AppDbContext> dbContextFactory)
{
    // Every month from the first pass to today, oldest first, empty months included
    // so the calendar scrolls without gaps.
    public async Task<ActivityCalendar> GetCalendarAsync(CancellationToken ct = default)
    {
        var events = await EventsAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var first = events.Count == 0 ? today : events.Min(e => e.Date);
        var oldest = new DateOnly(first.Year, first.Month, 1);

        var months = new List<ActivityMonth>();
        for (var cursor = oldest; cursor <= today; cursor = cursor.AddMonths(1))
        {
            var inMonth = events.Where(e => e.Date.Year == cursor.Year && e.Date.Month == cursor.Month).ToList();
            var days = inMonth
                .GroupBy(e => e.Date)
                .OrderBy(g => g.Key)
                .Select(g => BuildDay(g.Key, g.ToList()))
                .ToList();
            months.Add(new ActivityMonth(cursor.Year, cursor.Month, MonthName(cursor.Month),
                inMonth.Count(e => e.Kind != ActivityKind.Sat),
                inMonth.Count(e => e.Kind == ActivityKind.Finished),
                inMonth.Sum(e => e.Minutes ?? 0),
                days));
        }

        return new ActivityCalendar(months);
    }

    private static ActivityDay BuildDay(DateOnly date, List<ActivityEvent> events)
    {
        var loudest = events.OrderBy(e => KindRank(e.Kind)).ThenBy(e => e.Title).First();

        var shown = events
            .Where(e => !e.IsSilent)
            .OrderBy(e => KindRank(e.Kind))
            .ThenBy(e => e.Title)
            .ToList();

        var runs = events
            .Where(e => e.IsSilent)
            .GroupBy(e => e.UserMediaItemId)
            .Select(g => new ActivityRun(
                g.Key, g.First().Title, g.First().MediaType, g.First().ImageUrl,
                g.Count(e => e.Kind == ActivityKind.Progress),
                g.Sum(e => e.EffortDelta ?? 0),
                g.Sum(e => e.Minutes ?? 0)))
            .OrderBy(r => r.Title)
            .ToList();

        return new ActivityDay(date, loudest.ImageUrl, loudest.Title, loudest.Kind,
            events.Select(e => e.UserMediaItemId).Distinct().Count(),
            events.Sum(e => e.Minutes ?? 0),
            shown, runs);
    }

    private async Task<List<ActivityEvent>> EventsAsync(CancellationToken ct)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var entries = await db.ConsumptionEntries
            .Where(e => e.StartDate != null)
            .Include(e => e.Notes)
            .Include(e => e.Sessions)
            .Include(e => e.UserMediaItem!).ThenInclude(u => u.MediaItem)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(ct);

        // Passes linked by ResumesEntryId form one reading, so count chains,
        // not entries — resuming a dropped book is not a reread.
        var rereads = entries
            .GroupBy(e => e.UserMediaItemId)
            .SelectMany(g =>
            {
                var chain = -1;
                return g.OrderBy(e => e.StartDate).Select(e =>
                {
                    if (e.ResumesEntryId is null) chain++;
                    return (e.Id, IsReread: chain > 0);
                });
            })
            .Where(x => x.IsReread)
            .Select(x => x.Id)
            .ToHashSet();

        return entries
            .SelectMany(e => BuildEvents(e, rereads.Contains(e.Id)))
            .ToList();
    }

    private static IEnumerable<ActivityEvent> BuildEvents(ConsumptionEntry entry, bool isReread)
    {
        var media = entry.UserMediaItem!.MediaItem!;
        var image = media.DisplayImageUrl;
        var sessions = entry.Sessions.Where(s => s.EndedAt != null).ToList();

        ActivityEvent At(ActivityKind kind, DateOnly date, string? note,
            double? delta = null, double? effort = null, int? minutes = null) =>
            new(entry.UserMediaItemId, media.Title, media.MediaType, image, kind, date, note,
                kind == ActivityKind.Finished ? entry.RatingAtTime : null,
                entry.Context, delta, effort, media.Length, isReread, minutes);

        int? MinutesFor(EntryNote? note) =>
            note is null ? null : sessions.FirstOrDefault(s => s.EntryNoteId == note.Id)?.Minutes;

        // Milestones come from the pass's dates, not its notes — only finish
        // notes are guaranteed to exist.
        if (entry.StartDate is { } start)
            yield return At(
                entry.ResumesEntryId is null ? ActivityKind.Started : ActivityKind.Resumed,
                start, NoteOf(entry, NoteKind.Start)?.Text);

        foreach (var step in EffortMath.Walk(entry))
        {
            if (step.Note.Kind != NoteKind.Progress) continue;
            yield return At(ActivityKind.Progress, DateOnly.FromDateTime(step.When),
                step.Note.Text, step.Delta, step.Cumulative, MinutesFor(step.Note));
        }

        if (entry.EndDate is { } end)
        {
            var finish = NoteOf(entry, NoteKind.Finish);
            yield return At(
                entry.Outcome == PassOutcome.Dropped ? ActivityKind.Dropped : ActivityKind.Finished,
                end, finish?.Text, effort: entry.Effort, minutes: MinutesFor(finish));
        }

        foreach (var session in sessions.Where(s => s.EntryNoteId is null))
            yield return At(ActivityKind.Sat, EffortMath.LocalDay(session.StartedAt), null,
                minutes: session.Minutes);
    }

    private static EntryNote? NoteOf(ConsumptionEntry entry, NoteKind kind) => entry.Notes
        .Where(n => n.Kind == kind)
        .OrderBy(n => n.CreatedAt)
        .FirstOrDefault();

    private static int KindRank(ActivityKind kind) => kind switch
    {
        ActivityKind.Finished => 0,
        ActivityKind.Dropped => 1,
        ActivityKind.Started => 2,
        ActivityKind.Resumed => 3,
        ActivityKind.Progress => 4,
        _ => 5
    };

    private static string MonthName(int month) =>
        CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);
}
