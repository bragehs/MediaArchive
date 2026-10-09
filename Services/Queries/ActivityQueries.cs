using System.Globalization;
using MediaArchive.Data;
using MediaArchive.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services.Queries;

public enum ActivityKind { Started, Resumed, Progress, Finished, Dropped, Sat }

public record ActivityEvent
{
    public required int UserMediaItemId { get; init; }
    public required string Title { get; init; }
    public required MediaType MediaType { get; init; }
    public required string? ImageUrl { get; init; }
    public required ActivityKind Kind { get; init; }
    public required DateOnly Date { get; init; }
    public required string? Note { get; init; }
    public required int? Rating { get; init; }
    public required ConsumptionContext? Context { get; init; }
    public required double? EffortDelta { get; init; }
    public required double? EffortAtTime { get; init; }
    public required int? Length { get; init; }
    public required bool IsReread { get; init; }
    public required int? Minutes { get; init; }

    public bool IsMilestone => Kind is not (ActivityKind.Progress or ActivityKind.Sat);
    public bool IsSilent => !IsMilestone && string.IsNullOrWhiteSpace(Note);
}

public record ActivityRun(
    int UserMediaItemId,
    string Title,
    MediaType MediaType,
    string? ImageUrl,
    int Logs,
    double EffortDelta,
    int MinutesSat);

// Named ImageUrl because that is the property name the bridge resolves to a file.
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
    // Empty months included so the calendar scrolls without gaps.
    public async Task<ActivityCalendar> GetCalendarAsync(CancellationToken ct = default)
    {
        var events = await EventsAsync(ct);
        var today = EffortMath.Today;
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

        // Counts resume chains, not entries: resuming a dropped book is not a reread.
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
            .SelectMany(e => new PassEvents(e, rereads.Contains(e.Id)).All())
            .ToList();
    }

    private static int KindRank(ActivityKind kind) => kind switch
    {
        ActivityKind.Finished => 0,
        ActivityKind.Started => 1,
        ActivityKind.Resumed => 2,
        ActivityKind.Progress => 3,
        ActivityKind.Sat => 4,
        _ => 5
    };

    private static string MonthName(int month) =>
        CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);

    private sealed class PassEvents(ConsumptionEntry entry, bool isReread)
    {
        private readonly MediaItem _media = entry.UserMediaItem!.MediaItem!;
        private readonly List<Session> _sessions = entry.Sessions.Where(s => s.EndedAt != null).ToList();

        // From the pass's dates, not its notes — only finish notes are guaranteed to exist.
        public IEnumerable<ActivityEvent> All() =>
            Opening().Concat(Progress()).Concat(Closing()).Concat(Sittings());

        private IEnumerable<ActivityEvent> Opening()
        {
            if (entry.StartDate is not { } start) yield break;
            var kind = entry.ResumesEntryId is null ? ActivityKind.Started : ActivityKind.Resumed;
            yield return At(kind, start, NoteOf(NoteKind.Start)?.Text);
        }

        private IEnumerable<ActivityEvent> Progress() => EffortMath.Walk(entry)
            .Where(step => step.Note.Kind == NoteKind.Progress)
            .Select(step => At(ActivityKind.Progress, DateOnly.FromDateTime(step.When), step.Note.Text) with
            {
                EffortDelta = step.Delta,
                EffortAtTime = step.Cumulative,
                Minutes = MinutesFor(step.Note)
            });

        private IEnumerable<ActivityEvent> Closing()
        {
            if (entry.EndDate is not { } end) yield break;
            var finish = NoteOf(NoteKind.Finish);
            var kind = entry.Outcome == PassOutcome.Dropped ? ActivityKind.Dropped : ActivityKind.Finished;
            yield return At(kind, end, finish?.Text) with { EffortAtTime = entry.Effort, Minutes = MinutesFor(finish) };
        }

        private IEnumerable<ActivityEvent> Sittings() => _sessions
            .Where(s => s.EntryNoteId is null)
            .Select(s => At(ActivityKind.Sat, EffortMath.LocalDay(s.StartedAt), null) with { Minutes = s.Minutes });

        private ActivityEvent At(ActivityKind kind, DateOnly date, string? note) => new()
        {
            UserMediaItemId = entry.UserMediaItemId,
            Title = _media.Title,
            MediaType = _media.MediaType,
            ImageUrl = _media.DisplayImageUrl,
            Kind = kind,
            Date = date,
            Note = note,
            Rating = kind == ActivityKind.Finished ? entry.RatingAtTime : null,
            Context = entry.Context,
            EffortDelta = null,
            EffortAtTime = null,
            Length = _media.Length,
            IsReread = isReread,
            Minutes = null
        };

        private int? MinutesFor(EntryNote? note) =>
            note is null ? null : _sessions.FirstOrDefault(s => s.EntryNoteId == note.Id)?.Minutes;

        private EntryNote? NoteOf(NoteKind kind) => entry.Notes
            .Where(n => n.Kind == kind)
            .OrderBy(n => n.CreatedAt)
            .FirstOrDefault();
    }
}
