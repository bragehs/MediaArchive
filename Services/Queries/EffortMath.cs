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
