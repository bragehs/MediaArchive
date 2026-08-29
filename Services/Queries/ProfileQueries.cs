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
    IReadOnlyList<UniverseCard> Universes,
    IReadOnlyList<CreatorLine> Canon,
    PassRecord? LongestPass,
    PassRecord? FastestFinish,
    BailRecord? DeepestBail,
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
            LongestPass: BuildLongestPass(items),
            FastestFinish: BuildFastestFinish(items),
            DeepestBail: BuildDeepestBail(items),
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
