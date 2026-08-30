using MediaArchive.Data;
using MediaArchive.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services.Queries;

public record FameItem(int UserMediaItemId, string Title, string? ImageUrl,
    int Rating, bool IsFavorite);

public record UniverseCover(int UserMediaItemId, string Title, string? ImageUrl,
    MediaStatus Status);

// Effort in the same buckets Home reports: hours played, hours watched, pages
// read — universes mix media, and a single unit would silently drop the rest.
public record UniverseEffort(MediaBucket Bucket, double Value, string Unit);

public record UniverseCard(string Name, int Works, double? AvgRating, bool StillInside,
    IReadOnlyList<UniverseEffort> Effort, IReadOnlyList<UniverseCover> Covers);

public record CreatorLine(string Name, int Works, double? AvgRating,
    IReadOnlyList<MediaType> Types);

public record TypeRecord(string Label, string Value, int UserMediaItemId, string Title);

public record WeekBucket(DateOnly WeekStart, double Value);

// One toggle pane per media type: an effort-per-week progression in the type's
// native unit, and the extremes that make sense for that medium.
public record TypePanel(MediaType MediaType, string Unit,
    IReadOnlyList<WeekBucket> Weekly, IReadOnlyList<TypeRecord> Records);

public record MonthRecord(int Year, int Month, int Logs);

public record ProfileSnapshot(
    int ItemsLogged,
    double? AvgRating,
    int RatedCount,
    int Finished,
    int GenreCount,
    IReadOnlyList<FameItem> HallOfFame,
    IReadOnlyList<UniverseCard> Universes,
    IReadOnlyList<CreatorLine> Canon,
    IReadOnlyList<TypePanel> Panels,
    MonthRecord? BusiestMonth);

public class ProfileQueries(IDbContextFactory<AppDbContext> dbContextFactory)
{
    private const int FameFloor = 10;     // full marks only; favourites join regardless

    // One materialise, then every aggregate in memory: the archive is a single
    // local user's few hundred rows, and each section below is a different walk
    // over the same graph.
    public async Task<ProfileSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var items = await db.UserMediaItems
            .Include(u => u.MediaItem).ThenInclude(m => m!.Genres)
            .Include(u => u.MediaItem).ThenInclude(m => m!.Credits).ThenInclude(c => c.Person)
            .Include(u => u.MediaItem).ThenInclude(m => m!.Universe)
            .Include(u => u.Entries).ThenInclude(e => e.Notes)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(ct);

        var ratings = items.Where(u => u.Rating is not null).Select(u => u.Rating!.Value).ToList();

        var genreCount = items
            .SelectMany(u => u.MediaItem!.Genres)
            .Select(mg => mg.GenreId)
            .Distinct()
            .Count();

        return new ProfileSnapshot(
            ItemsLogged: items.Count,
            AvgRating: ratings.Count > 0 ? ratings.Average() : null,
            RatedCount: ratings.Count,
            Finished: items.Count(u => u.Status == MediaStatus.Completed),
            GenreCount: genreCount,
            HallOfFame: BuildHallOfFame(items),
            Universes: BuildUniverses(items),
            Canon: BuildCanon(items),
            Panels: BuildPanels(items, DateOnly.FromDateTime(DateTime.Today)),
            BusiestMonth: BuildBusiestMonth(items));
    }

    // The shelf is deliberately exclusive: favourites and full marks, nothing
    // else — and favourites lead it.
    private static List<FameItem> BuildHallOfFame(List<UserMediaItem> items) => items
        .Where(u => u.IsFavorite || u.Rating >= FameFloor)
        .OrderByDescending(u => u.IsFavorite)
        .ThenByDescending(u => u.Rating)
        .ThenBy(u => u.MediaItem!.Title)
        .Select(u => new FameItem(u.Id, u.MediaItem!.Title,
            u.MediaItem.LocalImagePath ?? u.MediaItem.ImageUrl,
            u.Rating ?? 0, u.IsFavorite))
        .ToList();

    private static List<UniverseCard> BuildUniverses(List<UserMediaItem> items) => items
        .Where(u => u.MediaItem!.Universe is not null)
        .GroupBy(u => u.MediaItem!.Universe!.Name)
        .Select(g =>
        {
            var rated = g.Where(u => u.Rating is not null).Select(u => u.Rating!.Value).ToList();

            double gamingMin = 0, viewingMin = 0, readingPages = 0;
            foreach (var u in g)
            {
                var media = u.MediaItem!;
                // A resumed pass carries its predecessor's total forward, so its
                // own contribution is the part above the baseline.
                var units = u.Entries.Sum(e => (e.Effort ?? 0) - (e.StartingEffort ?? 0));
                var minutes = media.MinutesPerUnit is { } perUnit ? units * perUnit : 0;

                switch (media.MediaType)
                {
                    case MediaType.Game: gamingMin += minutes; break;
                    case MediaType.Book: readingPages += units; break;
                    default: viewingMin += minutes; break;
                }
            }

            var effort = new List<UniverseEffort>();
            if (gamingMin > 0) effort.Add(new(MediaBucket.Gaming, Math.Round(gamingMin / 60, 1), "h played"));
            if (viewingMin > 0) effort.Add(new(MediaBucket.Viewing, Math.Round(viewingMin / 60, 1), "h watched"));
            if (readingPages > 0) effort.Add(new(MediaBucket.Reading, Math.Round(readingPages), "pages read"));

            // Your order: first touch first; the merely-interested trail the rail.
            var covers = g
                .OrderBy(u => u.Entries.Count == 0)
                .ThenBy(u => u.Entries.Select(e => e.StartDate).Min() ?? DateOnly.MaxValue)
                .Select(u => new UniverseCover(u.Id, u.MediaItem!.Title,
                    u.MediaItem.LocalImagePath ?? u.MediaItem.ImageUrl, u.Status))
                .ToList();

            return new UniverseCard(
                g.Key,
                g.Count(),
                rated.Count > 0 ? rated.Average() : null,
                g.Any(u => u.Entries.Any(e => e.StartDate is not null && e.EndDate is null)),
                effort,
                covers);
        })
        .OrderByDescending(c => c.Works)
        .ToList();

    // Only each medium's primary credit counts — otherwise one film trilogy
    // floods the list with its three screenwriters.
    private static List<CreatorLine> BuildCanon(List<UserMediaItem> items) => items
        .SelectMany(u => u.MediaItem!.Credits
            .Where(c => c.Person is not null
                        && c.Role == u.MediaItem!.MediaType.PrimaryCreditRole())
            .Select(c => (c.Person!.Name, u)))
        .GroupBy(x => x.Name)
        .Select(g =>
        {
            var rated = g.Select(x => x.u.Rating).Where(r => r is not null).ToList();
            return new CreatorLine(
                g.Key,
                g.Select(x => x.u.Id).Distinct().Count(),
                rated.Count > 0 ? rated.Average(r => r!.Value) : null,
                g.Select(x => x.u.MediaItem!.MediaType).Distinct().Order().ToList());
        })
        .OrderByDescending(c => c.Works)
        .ThenByDescending(c => c.AvgRating ?? 0)
        .ToList();

    private const int WeeklyWindow = 26;

    private static List<TypePanel> BuildPanels(List<UserMediaItem> items, DateOnly today) =>
        new[] { MediaType.Book, MediaType.Game, MediaType.Movie, MediaType.Show }
            .Select(t => BuildPanel(items.Where(u => u.MediaItem!.MediaType == t).ToList(), t, today))
            .Where(p => p.Records.Count > 0 || p.Weekly.Any(w => w.Value > 0))
            .ToList();

    private static TypePanel BuildPanel(List<UserMediaItem> items, MediaType type, DateOnly today)
    {
        var records = type switch
        {
            MediaType.Book => BookRecords(items),
            MediaType.Game => GameRecords(items),
            MediaType.Movie => MovieRecords(items),
            _ => ShowRecords(items)
        };

        return new TypePanel(type, UiHelpers.LengthUnit(type), BuildWeekly(items, today), records);
    }

    // Effort between two dated points is spread evenly across the days between
    // them — piecewise-linear, not a spike on the note's day. Where progress was
    // logged often the curve is sharp; a pass known only by its endpoints
    // degrades to its average pace instead of a cliff on the finish week.
    private static List<WeekBucket> BuildWeekly(List<UserMediaItem> items, DateOnly today)
    {
        var windowStart = StartOfWeek(today).AddDays(-7 * (WeeklyWindow - 1));
        var buckets = new double[WeeklyWindow];

        foreach (var entry in items.SelectMany(u => u.Entries))
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
                {
                    var day = toDay.AddDays(-d);
                    var week = (StartOfWeek(day).DayNumber - windowStart.DayNumber) / 7;
                    if (week >= 0 && week < WeeklyWindow)
                        buckets[week] += perDay;
                }
            }
        }

        return Enumerable.Range(0, WeeklyWindow)
            .Select(i => new WeekBucket(windowStart.AddDays(7 * i), Math.Round(buckets[i], 1)))
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
        ClosedPasses(List<UserMediaItem> items) => items
        .SelectMany(u => u.Entries, (u, e) => (Item: u, Entry: e))
        .Where(p => p.Entry.StartDate is not null && p.Entry.EndDate is not null)
        .Select(p => (p.Item, p.Entry,
            Days: p.Entry.EndDate!.Value.DayNumber - p.Entry.StartDate!.Value.DayNumber));

    private static TypeRecord? Bail(List<UserMediaItem> items, string per) => items
        .SelectMany(u => u.Entries, (u, e) => (Item: u, Entry: e))
        .Where(p => p.Entry.Outcome == PassOutcome.Dropped
                    && p.Entry.Effort is not null && p.Item.MediaItem!.Length is not null)
        .OrderByDescending(p => (double)p.Entry.Effort! / p.Item.MediaItem!.Length!.Value)
        .Select(p => new TypeRecord("Deepest bail",
            $"{(double)p.Entry.Effort! / p.Item.MediaItem!.Length!.Value:P0} in",
            p.Item.Id, p.Item.MediaItem.Title))
        .FirstOrDefault();

    private static List<TypeRecord> BookRecords(List<UserMediaItem> items)
    {
        var records = new List<TypeRecord?>();
        var finished = ClosedPasses(items)
            .Where(p => p.Entry.Outcome == PassOutcome.Completed
                        && p.Item.MediaItem!.Length is not null)
            .ToList();

        records.Add(finished
            .OrderByDescending(p => (double)p.Item.MediaItem!.Length! / Math.Max(1, p.Days))
            .Select(p => new TypeRecord("Fastest pace",
                $"{(double)p.Item.MediaItem!.Length! / Math.Max(1, p.Days):0.#} pages/day",
                p.Item.Id, p.Item.MediaItem.Title))
            .FirstOrDefault());

        records.Add(ClosedPasses(items)
            .OrderByDescending(p => p.Days)
            .Select(p => new TypeRecord("Longest read", $"{p.Days} days",
                p.Item.Id, p.Item.MediaItem!.Title))
            .FirstOrDefault());

        records.Add(finished
            .OrderByDescending(p => p.Item.MediaItem!.Length)
            .Select(p => new TypeRecord("Doorstop", $"{p.Item.MediaItem!.Length:#,0} pages",
                p.Item.Id, p.Item.MediaItem.Title))
            .FirstOrDefault());

        records.Add(Bail(items, "pages"));
        return records.Where(r => r is not null).Select(r => r!).ToList();
    }

    private static List<TypeRecord> GameRecords(List<UserMediaItem> items)
    {
        var records = new List<TypeRecord?>();

        records.Add(items
            .Select(u => (u, Hours: u.Entries.Sum(e => (e.Effort ?? 0) - (e.StartingEffort ?? 0))))
            .Where(x => x.Hours > 0)
            .OrderByDescending(x => x.Hours)
            .Select(x => new TypeRecord("Deepest sink", $"{x.Hours:#,0} h",
                x.u.Id, x.u.MediaItem!.Title))
            .FirstOrDefault());

        records.Add(ClosedPasses(items)
            .OrderByDescending(p => p.Days)
            .Select(p => new TypeRecord("Longest campaign", $"{p.Days} days",
                p.Item.Id, p.Item.MediaItem!.Title))
            .FirstOrDefault());

        // Your hours against the community estimate: rusher or completionist.
        records.Add(ClosedPasses(items)
            .Where(p => p.Entry.Outcome == PassOutcome.Completed
                        && p.Entry.Effort is not null && p.Item.MediaItem!.Length is > 0)
            .OrderByDescending(p => (double)p.Entry.Effort! / p.Item.MediaItem!.Length!.Value)
            .Select(p => new TypeRecord("Against the clock",
                $"{p.Entry.Effort} h vs {p.Item.MediaItem!.Length} h",
                p.Item.Id, p.Item.MediaItem.Title))
            .FirstOrDefault());

        records.Add(Bail(items, "hours"));
        return records.Where(r => r is not null).Select(r => r!).ToList();
    }

    private static List<TypeRecord> MovieRecords(List<UserMediaItem> items)
    {
        var records = new List<TypeRecord?>();

        records.Add(items
            .Where(u => u.MediaItem!.Length is not null && u.Entries.Any(e => e.EndDate is not null))
            .OrderByDescending(u => u.MediaItem!.Length)
            .Select(u => new TypeRecord("Longest sitting", $"{u.MediaItem!.Length} min",
                u.Id, u.MediaItem.Title))
            .FirstOrDefault());

        records.Add(items
            .Select(u => (u, Passes: u.Entries.Count(e => e.EndDate is not null)))
            .Where(x => x.Passes > 1)
            .OrderByDescending(x => x.Passes)
            .Select(x => new TypeRecord("Most rewatched", $"{x.Passes}×",
                x.u.Id, x.u.MediaItem!.Title))
            .FirstOrDefault());

        return records.Where(r => r is not null).Select(r => r!).ToList();
    }

    private static List<TypeRecord> ShowRecords(List<UserMediaItem> items)
    {
        var records = new List<TypeRecord?>();

        records.Add(items
            .Select(u => (u, Episodes: u.Entries.Sum(e => (e.Effort ?? 0) - (e.StartingEffort ?? 0))))
            .Where(x => x.Episodes > 0)
            .OrderByDescending(x => x.Episodes)
            .Select(x => new TypeRecord("Episode mountain", $"{x.Episodes:#,0} episodes",
                x.u.Id, x.u.MediaItem!.Title))
            .FirstOrDefault());

        records.Add(ClosedPasses(items)
            .Where(p => p.Entry.Outcome == PassOutcome.Completed && p.Entry.Effort is > 0)
            .OrderByDescending(p => (double)p.Entry.Effort! / Math.Max(1, p.Days))
            .Select(p => new TypeRecord("Fastest binge",
                $"{(double)p.Entry.Effort! / Math.Max(1, p.Days):0.#} eps/day",
                p.Item.Id, p.Item.MediaItem!.Title))
            .FirstOrDefault());

        records.Add(Bail(items, "episodes"));
        return records.Where(r => r is not null).Select(r => r!).ToList();
    }

    // A "log" here matches the Diary's event grammar: a start, a finish, and
    // every progress note each count once, on their own dates.
    private static MonthRecord? BuildBusiestMonth(List<UserMediaItem> items)
    {
        var counts = new Dictionary<(int Year, int Month), int>();
        void Bump(DateOnly d) =>
            counts[(d.Year, d.Month)] = counts.GetValueOrDefault((d.Year, d.Month)) + 1;

        foreach (var entry in items.SelectMany(u => u.Entries))
        {
            if (entry.StartDate is { } start) Bump(start);
            if (entry.EndDate is { } end) Bump(end);
            foreach (var step in EffortMath.Walk(entry))
                if (step.Note.Kind == NoteKind.Progress)
                    Bump(DateOnly.FromDateTime(step.When));
        }

        return counts.Count == 0
            ? null
            : counts.OrderByDescending(kv => kv.Value)
                .Select(kv => new MonthRecord(kv.Key.Year, kv.Key.Month, kv.Value))
                .First();
    }
}
