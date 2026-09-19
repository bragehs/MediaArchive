using MediaArchive.Models;

namespace MediaArchive.Services.Queries;

// Effort is stored cumulatively per note — a delta only exists relative to
// the running total before it.
public static class EffortMath
{
    public record NoteStep(EntryNote Note, DateTime When, double Delta, double Cumulative);

    public static IEnumerable<NoteStep> Walk(ConsumptionEntry entry)
    {
        // A resumed pass starts where the pass it continues left off.
        double previous = entry.StartingEffort ?? 0;
        foreach (var note in entry.Notes.OrderBy(n => n.CreatedAt))
        {
            var cumulative = (double)(note.EffortAtTime ?? previous);
            var delta = Math.Max(0, cumulative - previous);
            yield return new NoteStep(note, ActivityDate(entry, note), delta, cumulative);
            previous = cumulative;
        }
    }

    public static double UnitsLogged(ConsumptionEntry entry, DateTime from, DateTime toExclusive) =>
        Walk(entry).Where(s => s.When >= from && s.When < toExclusive).Sum(s => s.Delta);

    // Precedence: a logged Effort wins, above a resumed pass's baseline; else full Length.
    public static double? UnitsSpent(MediaItem media, ConsumptionEntry entry) =>
        entry.Effort is { } effort ? effort - (entry.StartingEffort ?? 0) : media.Length;

    // Null, never zero: a type that can't convert its unit (a show with no
    // episode runtime) has to leave an aggregate rather than deflate it.
    public static double? ToMinutes(MediaItem media, double units) =>
        media.MinutesPerUnit is { } perUnit ? units * perUnit : null;

    // The inverse of ToMinutes, floored: a prefill never claims more than was measured.
    public static int? UnitsFor(MediaItem media, double minutes) =>
        media.MinutesPerUnit is { } perUnit && perUnit > 0 ? (int)Math.Floor(minutes / perUnit) : null;

    // A film counts down its runtime, a show one episode; the open-ended types count up.
    public static int? SessionTarget(MediaItem media) => media switch
    {
        Movie => media.EstimatedMinutes is > 0 ? media.EstimatedMinutes : null,
        Show => media.MinutesPerUnit is > 0 ? (int)media.MinutesPerUnit.Value : null,
        _ => null
    };

    public record SessionSuggestion(int? Effort, double? HoursLeft, int? Runtime);

    // What a measured sitting implies for the log sheet, only where the conversion is
    // grounded in the item: exact for films and games, per-item for shows and audiobooks,
    // never the global page constant.
    public static SessionSuggestion Suggest(MediaItem media, ConsumptionEntry entry, int elapsedMinutes)
    {
        var previous = entry.Effort ?? 0;

        if (media is Book book)
        {
            if (entry.Context != ConsumptionContext.Audiobook || book.AudioHours is not > 0 || book.PageCount is not > 0)
                return new(null, null, null);

            var hours = book.AudioHours.Value;
            var left = hours - (double)previous / book.PageCount.Value * hours - elapsedMinutes / 60.0;
            return new(null, Math.Round(Math.Max(0, left), 1), null);
        }

        var effort = UnitsFor(media, elapsedMinutes) is { } units ? previous + units : (int?)null;
        // A film watched through has just measured the runtime the provider lacked.
        var runtime = media is Movie && media.EstimatedMinutes is not > 0 ? elapsedMinutes : (int?)null;
        return new(effort, null, runtime);
    }

    public static double? ProgressPercent(int? effort, int? length) =>
        effort is { } e && length is > 0 ? (double)e / length.Value * 100 : null;

    // Start/finish notes are filed under the pass's own date, so backfilled
    // entries land on the right day.
    public static DateTime ActivityDate(ConsumptionEntry entry, EntryNote note) => note.Kind switch
    {
        NoteKind.Finish => (entry.EndDate ?? DateOnly.FromDateTime(note.CreatedAt)).ToDateTime(TimeOnly.MinValue),
        NoteKind.Start => (entry.StartDate ?? DateOnly.FromDateTime(note.CreatedAt)).ToDateTime(TimeOnly.MinValue),
        _ => note.CreatedAt
    };
}
