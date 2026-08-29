using System.Globalization;
using MediaArchive.Data;
using MediaArchive.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services;

public enum DiaryEventKind { Started, Progress, Finished, Dropped }

// One entry per item touched in the month, carrying the loudest thing that
// happened to it — covers are the primary object, so the state rides on the
// cover as a glyph rather than in a separate column of marks.
public record DiaryTouch(
    int UserMediaItemId, string Title, string? ImageUrl,
    MediaType MediaType, DiaryEventKind Kind);

public record DiaryMonthSummary(
    int Month,
    string Name,
    int LogCount,
    int FinishedCount,
    IReadOnlyList<DiaryTouch> Touched);

public record DiaryYear(int Year, IReadOnlyList<DiaryMonthSummary> Months);

public record DiaryEvent(
    int UserMediaItemId,
    string Title,
    MediaType MediaType,
    string? ImageUrl,
    DiaryEventKind Kind,
    DateOnly Date,
    string? Note,
    int? Rating,
    ConsumptionContext? Context,
    double? EffortDelta,
    double? EffortAtTime,
    int? Length,
    bool IsReread)
{
    // Milestones carry the cover and the note as a pull-quote; a progress log is
    // a tick. A tick that has something to say still shows it — only the silent
    // ones are allowed to collapse into a run.
    public bool IsMilestone => Kind != DiaryEventKind.Progress;
    public bool IsSilent => Kind == DiaryEventKind.Progress && string.IsNullOrWhiteSpace(Note);
}

// A run of consecutive wordless ticks for one item, folded into a single line.
public record DiaryRun(
    int UserMediaItemId,
    string Title,
    MediaType MediaType,
    DateOnly From,
    DateOnly To,
    int Count,
    double EffortDelta);

public record DiaryDay(DateOnly Date, IReadOnlyList<DiaryEvent> Events, IReadOnlyList<DiaryRun> Runs);

public record DiaryMonthDetail(int Year, int Month, string Name,
    int LogCount, int FinishedCount, IReadOnlyList<DiaryDay> Days);

// Unlike ProfileQueries, which aggregates over the whole archive by definition,
// the diary is inherently windowed — one year or one month at a time. It is also
// the fastest-growing surface, since every progress log is a row.
public class DiaryQueries(IDbContextFactory<AppDbContext> dbContextFactory)
{
    public async Task<List<int>> GetYearsAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        // Projected then walked in memory: DateOnly.Year translation is not worth
        // relying on, and this is two short columns.
        var spans = await db.ConsumptionEntries
            .Where(e => e.StartDate != null)
            .Select(e => new { e.StartDate, e.EndDate })
            .AsNoTracking()
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var years = new HashSet<int>();
        foreach (var span in spans)
        {
            var last = (span.EndDate ?? today).Year;
            for (var y = span.StartDate!.Value.Year; y <= last; y++)
                years.Add(y);
        }

        return years.OrderByDescending(y => y).ToList();
    }

    public async Task<DiaryYear> GetYearAsync(int year, CancellationToken ct = default)
    {
        var events = await EventsInRangeAsync(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), ct);

        var months = events
            .GroupBy(e => e.Date.Month)
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var touched = g
                    .GroupBy(e => e.UserMediaItemId)
                    .Select(item => new
                    {
                        FirstTouch = item.Min(e => e.Date),
                        Loudest = item.OrderBy(e => KindRank(e.Kind)).First()
                    })
                    .OrderBy(x => x.FirstTouch)
                    .Select(x => new DiaryTouch(
                        x.Loudest.UserMediaItemId, x.Loudest.Title, x.Loudest.ImageUrl,
                        x.Loudest.MediaType, x.Loudest.Kind))
                    .ToList();

                return new DiaryMonthSummary(
                    Month: g.Key,
                    Name: MonthName(g.Key),
                    LogCount: g.Count(),
                    FinishedCount: g.Count(e => e.Kind == DiaryEventKind.Finished),
                    Touched: touched);
            })
            .ToList();

        return new DiaryYear(year, months);
    }

    public async Task<DiaryMonthDetail> GetMonthAsync(int year, int month,
        CancellationToken ct = default)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var events = await EventsInRangeAsync(from, to, ct);

        var days = events
            .GroupBy(e => e.Date)
            .OrderByDescending(g => g.Key)
            .Select(g => BuildDay(g.Key, g.ToList()))
            .ToList();

        return new DiaryMonthDetail(year, month, MonthName(month),
            events.Count, events.Count(e => e.Kind == DiaryEventKind.Finished), days);
    }

    // Within a day: milestones first (loudest), then ticks that said something,
    // then the silent ones folded per item into a single run.
    private static DiaryDay BuildDay(DateOnly date, List<DiaryEvent> events)
    {
        var shown = events
            .Where(e => !e.IsSilent)
            .OrderByDescending(e => e.IsMilestone)
            .ThenBy(e => e.Title)
            .ToList();

        var runs = events
            .Where(e => e.IsSilent)
            .GroupBy(e => e.UserMediaItemId)
            .Select(g => new DiaryRun(
                g.Key, g.First().Title, g.First().MediaType,
                date, date, g.Count(), g.Sum(e => e.EffortDelta ?? 0)))
            .OrderBy(r => r.Title)
            .ToList();

        return new DiaryDay(date, shown, runs);
    }

    private async Task<List<DiaryEvent>> EventsInRangeAsync(DateOnly from, DateOnly to,
        CancellationToken ct)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        // Any pass whose interval touches the window; its notes are inside it.
        var entries = await db.ConsumptionEntries
            .Where(e => e.StartDate != null && e.StartDate <= to
                        && (e.EndDate == null || e.EndDate >= from))
            .Include(e => e.Notes)
            .Include(e => e.UserMediaItem!).ThenInclude(u => u.MediaItem)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(ct);

        if (entries.Count == 0) return [];

        // Pass ordinal has to come from an item's whole history, not the window,
        // or the first pass inside a given month would look like the first ever.
        var itemIds = entries.Select(e => e.UserMediaItemId).Distinct().ToList();
        var allPasses = await db.ConsumptionEntries
            .Where(e => itemIds.Contains(e.UserMediaItemId))
            .Select(e => new { e.Id, e.UserMediaItemId, e.StartDate })
            .AsNoTracking()
            .ToListAsync(ct);

        var rereads = allPasses
            .GroupBy(p => p.UserMediaItemId)
            .SelectMany(g => g.OrderBy(p => p.StartDate).Skip(1))
            .Select(p => p.Id)
            .ToHashSet();

        return entries
            .SelectMany(e => BuildEvents(e, rereads.Contains(e.Id)))
            .Where(e => e.Date >= from && e.Date <= to)
            .ToList();
    }

    private static IEnumerable<DiaryEvent> BuildEvents(ConsumptionEntry entry, bool isReread)
    {
        var media = entry.UserMediaItem!.MediaItem!;
        var image = media.LocalImagePath ?? media.ImageUrl;

        DiaryEvent At(DiaryEventKind kind, DateOnly date, string? note,
            double? delta = null, double? effort = null) =>
            new(entry.UserMediaItemId, media.Title, media.MediaType, image, kind, date, note,
                kind == DiaryEventKind.Finished ? entry.RatingAtTime : null,
                entry.Context, delta, effort, media.Length, isReread);

        // Milestones come from the pass's own dates, not from notes: a note is
        // only required on finish, so a note-driven feed would silently lose
        // most "started" events.
        if (entry.StartDate is { } start)
            yield return At(DiaryEventKind.Started, start, TextOf(entry, NoteKind.Start));

        foreach (var step in EffortMath.Walk(entry))
        {
            if (step.Note.Kind != NoteKind.Progress) continue;
            yield return At(DiaryEventKind.Progress, DateOnly.FromDateTime(step.When),
                step.Note.Text, step.Delta, step.Cumulative);
        }

        if (entry.EndDate is { } end)
            yield return At(
                entry.Outcome == PassOutcome.Dropped ? DiaryEventKind.Dropped : DiaryEventKind.Finished,
                end, TextOf(entry, NoteKind.Finish), effort: entry.Effort);
    }

    private static string? TextOf(ConsumptionEntry entry, NoteKind kind) => entry.Notes
        .Where(n => n.Kind == kind && !string.IsNullOrWhiteSpace(n.Text))
        .OrderBy(n => n.CreatedAt)
        .Select(n => n.Text)
        .FirstOrDefault();

    // Finishing beats dropping beats starting beats a bare log.
    private static int KindRank(DiaryEventKind kind) => kind switch
    {
        DiaryEventKind.Finished => 0,
        DiaryEventKind.Dropped => 1,
        DiaryEventKind.Started => 2,
        _ => 3
    };

    private static string MonthName(int month) =>
        CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);
}
