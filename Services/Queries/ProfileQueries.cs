using MediaArchive.Data;
using MediaArchive.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services.Queries;

public record FameItem(int UserMediaItemId, string Title, string? ImageUrl,
    int Rating, bool IsFavorite);

// Personal rating against the public one, both on the stored /10 scale;
// halving to stars is the view's job, like everywhere else.
public record Verdict(int UserMediaItemId, string Title, MediaType MediaType,
    int Mine, double World, double Delta);

public record CreatorLine(string Name, int Works, double? AvgRating,
    IReadOnlyList<MediaType> Types);

public record PassRecord(int UserMediaItemId, string Title, MediaType MediaType, int Days);

public record BailRecord(int UserMediaItemId, string Title, MediaType MediaType, double Share);

public record MonthRecord(int Year, int Month, int Logs);

public record ProfileSnapshot(
    int ItemsLogged,
    double? AvgRating,
    int RatedCount,
    int Finished,
    int GenreCount,
    IReadOnlyList<FameItem> HallOfFame,
    IReadOnlyList<Verdict> Verdicts,
    double? AvgDelta,
    IReadOnlyList<CreatorLine> Canon,
    PassRecord? LongestPass,
    PassRecord? FastestFinish,
    BailRecord? DeepestBail,
    MonthRecord? BusiestMonth);

public class ProfileQueries(IDbContextFactory<AppDbContext> dbContextFactory)
{
    private const int FameFloor = 9;      // 4.5★ and up make the shelf
    private const int VerdictCap = 8;
    private const int CanonCap = 6;

    // One materialise, then every aggregate in memory: the archive is a single
    // local user's few hundred rows, and each section below is a different walk
    // over the same graph.
    public async Task<ProfileSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var items = await db.UserMediaItems
            .Include(u => u.MediaItem).ThenInclude(m => m!.Genres)
            .Include(u => u.MediaItem).ThenInclude(m => m!.Credits).ThenInclude(c => c.Person)
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

        var verdicts = BuildVerdicts(items);

        return new ProfileSnapshot(
            ItemsLogged: items.Count,
            AvgRating: ratings.Count > 0 ? ratings.Average() : null,
            RatedCount: ratings.Count,
            Finished: items.Count(u => u.Status == MediaStatus.Completed),
            GenreCount: genreCount,
            HallOfFame: BuildHallOfFame(items),
            Verdicts: verdicts.Take(VerdictCap).ToList(),
            AvgDelta: verdicts.Count > 0 ? verdicts.Average(v => v.Delta) : null,
            Canon: BuildCanon(items),
            LongestPass: BuildLongestPass(items),
            FastestFinish: BuildFastestFinish(items),
            DeepestBail: BuildDeepestBail(items),
            BusiestMonth: BuildBusiestMonth(items));
    }

    // Favourites always belong; ratings from the floor up join them. Favourites
    // and full marks lead the shelf, the 4.5s trail it.
    private static List<FameItem> BuildHallOfFame(List<UserMediaItem> items) => items
        .Where(u => u.IsFavorite || u.Rating >= FameFloor)
        .OrderByDescending(u => u.IsFavorite)
        .ThenByDescending(u => u.Rating)
        .ThenBy(u => u.MediaItem!.Title)
        .Select(u => new FameItem(u.Id, u.MediaItem!.Title,
            u.MediaItem.LocalImagePath ?? u.MediaItem.ImageUrl,
            u.Rating ?? 0, u.IsFavorite))
        .ToList();

    private static List<Verdict> BuildVerdicts(List<UserMediaItem> items) => items
        .Where(u => u.Rating is not null && u.MediaItem!.ExternalRating is not null)
        .Select(u => new Verdict(u.Id, u.MediaItem!.Title, u.MediaItem.MediaType,
            u.Rating!.Value, u.MediaItem.ExternalRating!.Value,
            u.Rating.Value - u.MediaItem.ExternalRating.Value))
        .OrderByDescending(v => Math.Abs(v.Delta))
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
        .Take(CanonCap)
        .ToList();

    private static IEnumerable<(UserMediaItem Item, ConsumptionEntry Entry, int Days)>
        ClosedPasses(List<UserMediaItem> items) => items
        .SelectMany(u => u.Entries, (u, e) => (Item: u, Entry: e))
        .Where(p => p.Entry.StartDate is not null && p.Entry.EndDate is not null)
        .Select(p => (p.Item, p.Entry,
            Days: p.Entry.EndDate!.Value.DayNumber - p.Entry.StartDate!.Value.DayNumber));

    private static PassRecord? BuildLongestPass(List<UserMediaItem> items) =>
        ClosedPasses(items)
            .OrderByDescending(p => p.Days)
            .Select(p => new PassRecord(p.Item.Id, p.Item.MediaItem!.Title,
                p.Item.MediaItem.MediaType, p.Days))
            .FirstOrDefault();

    // Films finish in a sitting by nature, so they'd hold this record forever;
    // it only means something for media consumed across days.
    private static PassRecord? BuildFastestFinish(List<UserMediaItem> items) =>
        ClosedPasses(items)
            .Where(p => p.Entry.Outcome == PassOutcome.Completed
                        && p.Item.MediaItem!.MediaType != MediaType.Movie)
            .OrderBy(p => p.Days)
            .Select(p => new PassRecord(p.Item.Id, p.Item.MediaItem!.Title,
                p.Item.MediaItem.MediaType, p.Days))
            .FirstOrDefault();

    private static BailRecord? BuildDeepestBail(List<UserMediaItem> items) => items
        .SelectMany(u => u.Entries, (u, e) => (Item: u, Entry: e))
        .Where(p => p.Entry.Outcome == PassOutcome.Dropped
                    && p.Entry.Effort is not null && p.Item.MediaItem!.Length is not null)
        .Select(p => new BailRecord(p.Item.Id, p.Item.MediaItem!.Title,
            p.Item.MediaItem.MediaType,
            (double)p.Entry.Effort!.Value / p.Item.MediaItem.Length!.Value))
        .OrderByDescending(b => b.Share)
        .FirstOrDefault();

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
