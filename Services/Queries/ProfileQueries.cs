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

public record UniverseCard(string Name, int Works, double? AvgRating,
    IReadOnlyList<UniverseEffort> Effort, IReadOnlyList<UniverseCover> Covers);

public record CreatorLine(string Name, int Works, double? AvgRating,
    IReadOnlyList<MediaType> Types);

public record MonthRecord(int Year, int Month, int Logs);

// One medium's minutes in one year. Filled by the same walk as the totals, so
// the mix and the totals cannot drift apart.
public record TimeBucket(MediaType MediaType, int Year, double Minutes);

// Measured and derived minutes stay apart so a largely estimated figure renders
// as "≈352 h" rather than passing itself off as counted.
public record TimeSpent(double ActualMinutes, double EstimatedMinutes,
    int Items, int WithoutLength, IReadOnlyList<TimeBucket> Buckets);

public record ProfileSnapshot(
    TimeSpent TimeSpent,
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
            TimeSpent: BuildTimeSpent(items),
            ItemsLogged: items.Count,
            AvgRating: ratings.Count > 0 ? ratings.Average() : null,
            RatedCount: ratings.Count,
            Finished: items.Count(u => u.Status == MediaStatus.Completed),
            GenreCount: genreCount,
            HallOfFame: BuildHallOfFame(items),
            Universes: BuildUniverses(items),
            Canon: BuildCanon(items),
            Panels: ProfilePanels.Build(items, DateOnly.FromDateTime(DateTime.Today)),
            BusiestMonth: BuildBusiestMonth(items));
    }

    // Minutes is the one unit all four types convert to. An item whose length
    // won't convert leaves the total AND the denominator: counted as zero it
    // would quietly deflate every average built on this.
    private static TimeSpent BuildTimeSpent(List<UserMediaItem> items)
    {
        double actual = 0, estimated = 0;
        int counted = 0, dropped = 0;
        var buckets = new Dictionary<(MediaType Type, int Year), double>();

        foreach (var item in items)
        {
            var media = item.MediaItem!;
            var contributed = false;

            foreach (var entry in item.Entries)
            {
                if (EffortMath.UnitsSpent(media, entry) is not { } units
                    || EffortMath.ToMinutes(media, units) is not { } minutes)
                    continue;

                if (entry.Effort is null) estimated += minutes;
                else actual += minutes;
                contributed = true;

                var key = (media.MediaType, PassYear(item, entry));
                buckets[key] = buckets.GetValueOrDefault(key) + minutes;
            }

            if (contributed) counted++;
            else if (item.Entries.Count > 0) dropped++;
        }

        return new TimeSpent(actual, estimated, counted, dropped,
            buckets
                .OrderBy(b => b.Key.Year).ThenBy(b => b.Key.Type)
                .Select(b => new TimeBucket(b.Key.Type, b.Key.Year, b.Value))
                .ToList());
    }

    // A pass lands in the year it closed, whole. Estimated minutes carry no dates
    // to spread across, and spreading them would invent a pace the archive never
    // recorded — the fiction IsLive keeps out of the records.
    private static int PassYear(UserMediaItem item, ConsumptionEntry entry) =>
        (entry.EndDate ?? entry.StartDate ?? item.AddedDate).Year;

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
                var units = u.Entries.Sum(e => EffortMath.UnitsSpent(media, e) ?? 0);
                var minutes = EffortMath.ToMinutes(media, units) ?? 0;

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
