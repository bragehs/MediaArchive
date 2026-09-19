using MediaArchive.Models;

namespace MediaArchive.Services.Queries;

public record TypeRecord(string Label, string Value, int UserMediaItemId, string Title);

public record PanelStat(string Value, string Label);

public record WeekBucket(DateOnly WeekStart, double Value);

public record YearBucket(int Year, double Value);

// How a medium reached you: print against audiobook, console against PC. A null
// Context is its own share — unrecorded is a gap to fill, not an absence.
public record ContextShare(ConsumptionContext? Context, int Passes);

// Books only: pages over the span, against your own median. Recalled means the
// two dates it divides by were remembered afterwards rather than logged as they
// happened — the same arithmetic, softer inputs.
public record PaceRow(int UserMediaItemId, string Title, double PagesPerDay, bool Recalled);

// Games only: the hours you put in against the community's time to beat it.
public record EstimateRow(int UserMediaItemId, string Title, int Hours, int Estimate);

// One toggle pane per media type: an effort-per-week progression in the type's
// native unit, the shape only that medium has, and its own extremes.
public record TypePanel(MediaType MediaType, string Unit, IReadOnlyList<PanelStat> Stats,
    IReadOnlyList<WeekBucket> Weekly, IReadOnlyList<YearBucket> Yearly,
    IReadOnlyList<ContextShare> Contexts, IReadOnlyList<PaceRow> Pace, double? PaceMedian,
    IReadOnlyList<EstimateRow> Estimates, IReadOnlyList<TypeRecord> Records);

// The per-medium half of the profile: one pane each, in that medium's own unit.
// Split from ProfileQueries because the two ask different questions of the same
// rows — the snapshot aggregates the whole archive, this walks it a type at a time.
internal static class ProfilePanels
{
    public static List<TypePanel> Build(List<UserMediaItem> items, DateOnly today) =>
        new[] { MediaType.Book, MediaType.Game, MediaType.Movie, MediaType.Show }
            .Select(t => BuildPanel(items
                .Where(u => u.MediaItem!.MediaType == t)
                .SelectMany(u => u.Entries, (u, e) => (Item: u, Entry: e))
                .Where(p => p.Entry.StartDate is not null)
                .ToList(), t, today))
            .Where(p => p.Records.Count > 0 || p.Weekly.Any(w => w.Value > 0))
            .ToList();

    // Whether a pass's dates were recorded as it happened rather than remembered
    // afterwards: still open, ended on or after the item was added, or carrying a
    // real progress note. Single-sitting passes get two weeks of grace on the add
    // date — with start == end there is no span to misremember.
    //
    // This marks, it no longer filters. A rate over a recalled span is soft, not
    // false, and an archive that is mostly reconstructed has a history worth
    // drawing: PaceRow carries the flag through to the UI instead. The one
    // survivor is the binge record below, which has no chart to caveat it in.
    private static bool IsLive(UserMediaItem item, ConsumptionEntry entry) =>
        entry.EndDate is null
        || entry.EndDate >= item.AddedDate
        || entry.Notes.Any(n => n.Kind == NoteKind.Progress)
        || (entry.StartDate == entry.EndDate
            && entry.EndDate >= item.AddedDate.AddDays(-14));

    private static TypePanel BuildPanel(List<(UserMediaItem Item, ConsumptionEntry Entry)> passes,
        MediaType type, DateOnly today)
    {
        var records = type switch
        {
            MediaType.Book => BookRecords(passes),
            MediaType.Game => GameRecords(passes),
            MediaType.Movie => MovieRecords(passes),
            _ => ShowRecords(passes)
        };

        var pace = type == MediaType.Book ? BuildPace(passes) : [];

        return new TypePanel(type, UiHelpers.LengthUnit(type),
            BuildStats(passes, type), BuildWeekly(passes, today),
            BuildYearly(passes, today), BuildContexts(passes),
            pace, Median(pace.Select(p => p.PagesPerDay).ToList()),
            type == MediaType.Game ? BuildEstimates(passes) : [],
            records);
    }

    // Every finished read, recalled ones marked rather than dropped: the archive is
    // mostly reconstructed, and filtering left one row out of seven.
    private static List<PaceRow> BuildPace(
        List<(UserMediaItem Item, ConsumptionEntry Entry)> passes) => ClosedPasses(passes)
        .Where(p => p.Entry.Outcome == PassOutcome.Completed
                    && p.Item.MediaItem!.Length is > 0)
        .Select(p => new PaceRow(p.Item.Id, p.Item.MediaItem!.Title,
            (double)p.Item.MediaItem.Length!.Value / Math.Max(1, p.Days),
            !IsLive(p.Item, p.Entry)))
        .OrderByDescending(p => p.PagesPerDay)
        .ToList();

    // No IsLive here: this divides nothing by a span. Hours against the community
    // estimate is two totals, and a remembered total is a fact backfill keeps.
    private static List<EstimateRow> BuildEstimates(
        List<(UserMediaItem Item, ConsumptionEntry Entry)> passes) => ClosedPasses(passes)
        .Where(p => p.Entry.Outcome == PassOutcome.Completed
                    && p.Entry.Effort is not null
                    && p.Item.MediaItem!.Length is > 0)
        .Select(p => new EstimateRow(p.Item.Id, p.Item.MediaItem!.Title,
            p.Entry.Effort!.Value, p.Item.MediaItem.Length!.Value))
        .OrderByDescending(e => e.Hours)
        .ToList();

    private static double? Median(List<double> values)
    {
        if (values.Count == 0) return null;
        var sorted = values.Order().ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    private static List<ContextShare> BuildContexts(
        List<(UserMediaItem Item, ConsumptionEntry Entry)> passes) => passes
        .GroupBy(p => p.Entry.Context)
        .Select(g => new ContextShare(g.Key, g.Count()))
        .OrderByDescending(c => c.Context is not null)
        .ThenByDescending(c => c.Passes)
        .ToList();

    // The headline totals above the chart, all from live passes only.
    private static List<PanelStat> BuildStats(
        List<(UserMediaItem Item, ConsumptionEntry Entry)> passes, MediaType type)
    {
        var total = passes.Sum(p => (p.Entry.Effort ?? 0) - (p.Entry.StartingEffort ?? 0));
        var finished = passes.Count(p => p.Entry.Outcome == PassOutcome.Completed);
        var rated = passes
            .Select(p => p.Item)
            .Distinct()
            .Where(u => u.Rating is not null)
            .Select(u => u.Rating!.Value)
            .ToList();

        var stats = new List<PanelStat>();
        if (total > 0)
            stats.Add(new($"{total:#,0}", UiHelpers.LengthUnit(type)));
        stats.Add(new(finished.ToString(), "finished"));
        if (rated.Count > 0)
            stats.Add(new((rated.Average() / 2).ToString("0.#",
                System.Globalization.CultureInfo.InvariantCulture), "avg ★"));
        return stats;
    }

    // Effort between two dated points is spread evenly across the days between
    // them — piecewise-linear, not a spike on the note's day. Where progress was
    // logged often the curve is sharp; a pass known only by its endpoints
    // degrades to its average pace instead of a cliff on the finish week.
    private static IEnumerable<(DateOnly Day, double Amount)> DailyEffort(
        List<(UserMediaItem Item, ConsumptionEntry Entry)> passes, DateOnly today)
    {
        foreach (var (_, entry) in passes)
        {
            var points = EffortPoints(entry, today);
            for (var k = 1; k < points.Count; k++)
            {
                var (fromDay, fromVal) = points[k - 1];
                var (toDay, toVal) = points[k];
                var delta = toVal - fromVal;
                if (delta <= 0) continue;

                var days = Math.Max(1, toDay.DayNumber - fromDay.DayNumber);
                var perDay = delta / days;
                for (var d = 0; d < days; d++)
                    yield return (toDay.AddDays(-d), perDay);
            }
        }
    }

    // The current year, week by week.
    private static List<WeekBucket> BuildWeekly(
        List<(UserMediaItem Item, ConsumptionEntry Entry)> passes, DateOnly today)
    {
        var windowStart = StartOfWeek(new DateOnly(today.Year, 1, 1));
        var weeks = (StartOfWeek(today).DayNumber - windowStart.DayNumber) / 7 + 1;
        var buckets = new double[weeks];

        foreach (var (day, amount) in DailyEffort(passes, today))
        {
            var week = (StartOfWeek(day).DayNumber - windowStart.DayNumber) / 7;
            if (week >= 0 && week < weeks)
                buckets[week] += amount;
        }

        return Enumerable.Range(0, weeks)
            .Select(i => new WeekBucket(windowStart.AddDays(7 * i), Math.Round(buckets[i], 1)))
            .ToList();
    }

    // The whole archive, year by year — gaps included, so a quiet decade stays
    // visibly quiet instead of being edited out.
    private static List<YearBucket> BuildYearly(
        List<(UserMediaItem Item, ConsumptionEntry Entry)> passes, DateOnly today)
    {
        if (passes.Count == 0) return [];

        var first = passes.Min(p => p.Entry.StartDate!.Value.Year);
        var buckets = new double[today.Year - first + 1];

        foreach (var (day, amount) in DailyEffort(passes, today))
        {
            var i = day.Year - first;
            if (i >= 0 && i < buckets.Length)
                buckets[i] += amount;
        }

        return Enumerable.Range(0, buckets.Length)
            .Select(i => new YearBucket(first + i, Math.Round(buckets[i], 1)))
            .ToList();
    }

    // The dated cumulative-effort points of one pass: its start (at the resumed
    // baseline), every note that carries effort, and — for passes that recorded
    // no notes, like a film logged in one sitting — the closing total itself.
    private static List<(DateOnly Day, double Value)> EffortPoints(ConsumptionEntry entry, DateOnly today)
    {
        var points = new List<(DateOnly, double)>();
        if (entry.StartDate is not { } start) return points;

        points.Add((start, entry.StartingEffort ?? 0));

        foreach (var step in EffortMath.Walk(entry))
            if (step.Note.EffortAtTime is not null)
                points.Add((DateOnly.FromDateTime(step.When), step.Cumulative));

        if (entry.Effort is { } total && total > points[^1].Item2)
            points.Add((entry.EndDate ?? today, total));

        return points;
    }

    private static DateOnly StartOfWeek(DateOnly day) =>
        day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    private static IEnumerable<(UserMediaItem Item, ConsumptionEntry Entry, int Days)>
        ClosedPasses(List<(UserMediaItem Item, ConsumptionEntry Entry)> passes) => passes
        .Where(p => p.Entry.StartDate is not null && p.Entry.EndDate is not null)
        .Select(p => (p.Item, p.Entry,
            Days: p.Entry.EndDate!.Value.DayNumber - p.Entry.StartDate!.Value.DayNumber));

    private static TypeRecord? Bail(List<(UserMediaItem Item, ConsumptionEntry Entry)> passes,
        string per) => passes
        .Where(p => p.Entry.Outcome == PassOutcome.Dropped
                    && p.Entry.Effort is not null && p.Item.MediaItem!.Length is not null)
        .OrderByDescending(p => (double)p.Entry.Effort! / p.Item.MediaItem!.Length!.Value)
        .Select(p => new TypeRecord("Deepest bail",
            $"{(double)p.Entry.Effort! / p.Item.MediaItem!.Length!.Value:P0} in",
            p.Item.Id, p.Item.MediaItem.Title))
        .FirstOrDefault();

    private static List<TypeRecord> BookRecords(List<(UserMediaItem Item, ConsumptionEntry Entry)> passes)
    {
        var records = new List<TypeRecord?>();
        var finished = ClosedPasses(passes)
            .Where(p => p.Entry.Outcome == PassOutcome.Completed
                        && p.Item.MediaItem!.Length is not null)
            .ToList();

        records.Add(finished
            .OrderByDescending(p => (double)p.Item.MediaItem!.Length! / Math.Max(1, p.Days))
            .Select(p => new TypeRecord("Fastest pace",
                $"{(double)p.Item.MediaItem!.Length! / Math.Max(1, p.Days):0.#} pages/day",
                p.Item.Id, p.Item.MediaItem.Title))
            .FirstOrDefault());

        records.Add(ClosedPasses(passes)
            .OrderByDescending(p => p.Days)
            .Select(p => new TypeRecord("Longest read", $"{p.Days} days",
                p.Item.Id, p.Item.MediaItem!.Title))
            .FirstOrDefault());

        records.Add(finished
            .OrderByDescending(p => p.Item.MediaItem!.Length)
            .Select(p => new TypeRecord("Doorstop", $"{p.Item.MediaItem!.Length:#,0} pages",
                p.Item.Id, p.Item.MediaItem.Title))
            .FirstOrDefault());

        records.Add(Bail(passes, "pages"));
        return records.Where(r => r is not null).Select(r => r!).ToList();
    }

    private static List<TypeRecord> GameRecords(List<(UserMediaItem Item, ConsumptionEntry Entry)> passes)
    {
        var records = new List<TypeRecord?>();

        records.Add(passes
            .GroupBy(p => p.Item)
            .Select(g => (u: g.Key, Hours: g.Sum(p => (p.Entry.Effort ?? 0) - (p.Entry.StartingEffort ?? 0))))
            .Where(x => x.Hours > 0)
            .OrderByDescending(x => x.Hours)
            .Select(x => new TypeRecord("Deepest sink", $"{x.Hours:#,0} h",
                x.u.Id, x.u.MediaItem!.Title))
            .FirstOrDefault());

        records.Add(ClosedPasses(passes)
            .OrderByDescending(p => p.Days)
            .Select(p => new TypeRecord("Longest campaign", $"{p.Days} days",
                p.Item.Id, p.Item.MediaItem!.Title))
            .FirstOrDefault());

        records.Add(Bail(passes, "hours"));
        return records.Where(r => r is not null).Select(r => r!).ToList();
    }

    private static List<TypeRecord> MovieRecords(List<(UserMediaItem Item, ConsumptionEntry Entry)> passes)
    {
        var records = new List<TypeRecord?>();

        records.Add(passes
            .Where(p => p.Item.MediaItem!.Length is not null && p.Entry.EndDate is not null)
            .OrderByDescending(p => p.Item.MediaItem!.Length)
            .Select(p => new TypeRecord("Longest sitting", $"{p.Item.MediaItem!.Length} min",
                p.Item.Id, p.Item.MediaItem.Title))
            .FirstOrDefault());

        records.Add(passes
            .GroupBy(p => p.Item)
            .Select(g => (u: g.Key, Passes: g.Count(p => p.Entry.EndDate is not null)))
            .Where(x => x.Passes > 1)
            .OrderByDescending(x => x.Passes)
            .Select(x => new TypeRecord("Most rewatched", $"{x.Passes}×",
                x.u.Id, x.u.MediaItem!.Title))
            .FirstOrDefault());

        return records.Where(r => r is not null).Select(r => r!).ToList();
    }

    private static List<TypeRecord> ShowRecords(List<(UserMediaItem Item, ConsumptionEntry Entry)> passes)
    {
        var records = new List<TypeRecord?>();

        records.Add(passes
            .GroupBy(p => p.Item)
            .Select(g => (u: g.Key, Episodes: g.Sum(p => (p.Entry.Effort ?? 0) - (p.Entry.StartingEffort ?? 0))))
            .Where(x => x.Episodes > 0)
            .OrderByDescending(x => x.Episodes)
            .Select(x => new TypeRecord("Episode mountain", $"{x.Episodes:#,0} episodes",
                x.u.Id, x.u.MediaItem!.Title))
            .FirstOrDefault());

        records.Add(ClosedPasses(passes)
            .Where(p => IsLive(p.Item, p.Entry))
            .Where(p => p.Entry.Outcome == PassOutcome.Completed && p.Entry.Effort is > 0)
            .OrderByDescending(p => (double)p.Entry.Effort! / Math.Max(1, p.Days))
            .Select(p => new TypeRecord("Fastest binge",
                $"{(double)p.Entry.Effort! / Math.Max(1, p.Days):0.#} eps/day",
                p.Item.Id, p.Item.MediaItem!.Title))
            .FirstOrDefault());

        records.Add(Bail(passes, "episodes"));
        return records.Where(r => r is not null).Select(r => r!).ToList();
    }

}
