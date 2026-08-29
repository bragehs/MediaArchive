using MediaArchive.Models;

namespace MediaArchive.Services;

// Effort is stored cumulatively on each note, so a note means nothing on its
// own — the amount logged only exists relative to the running total before it.
// Home needs a windowed sum of that walk; Diary needs each individual step.
public static class EffortMath
{
    public record NoteStep(EntryNote Note, DateTime When, double Delta, double Cumulative);

    public static IEnumerable<NoteStep> Walk(ConsumptionEntry entry)
    {
        double previous = 0;
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

    // A start or finish note is filed under the pass's own date, not the moment
    // it was typed — backfilled entries would otherwise land on the wrong day.
    public static DateTime ActivityDate(ConsumptionEntry entry, EntryNote note) => note.Kind switch
    {
        NoteKind.Finish => (entry.EndDate ?? DateOnly.FromDateTime(note.CreatedAt)).ToDateTime(TimeOnly.MinValue),
        NoteKind.Start => (entry.StartDate ?? DateOnly.FromDateTime(note.CreatedAt)).ToDateTime(TimeOnly.MinValue),
        _ => note.CreatedAt
    };
}
