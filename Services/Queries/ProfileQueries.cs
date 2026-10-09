using MediaArchive.Data;
using MediaArchive.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services.Queries;

public record FameItem(int UserMediaItemId, string Title, string? ImageUrl,
    int Rating, bool IsFavorite);

public record UniverseCover(int UserMediaItemId, string Title, string? ImageUrl,
    MediaStatus Status);

// Per bucket, not one unit: universes mix media.
public record UniverseEffort(MediaBucket Bucket, double Value, string Unit);

public record UniverseCard(string Name, int Works, double? AvgRating,
    IReadOnlyList<UniverseEffort> Effort, IReadOnlyList<UniverseCover> Covers);

public record CreatorLine(string Name, int Works, double? AvgRating,
    IReadOnlyList<MediaType> Types);

public record MonthRecord(int Year, int Month, int Logs);

// Filled by the same walk as the totals, so the mix and the totals cannot drift apart.
public record TimeBucket(MediaType MediaType, int Year, double Minutes);

// Measured and derived minutes stay apart so an estimated figure renders as ≈.
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
    private const int FameFloor = 10;

    // One materialise, then every aggregate in memory: a single user's few hundred rows.
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

    // An unconvertible length leaves the denominator too, or it would deflate every average.
    private static TimeSpent BuildTimeSpent(List<UserMediaItem> items)
    {
        var timed = items.Select(u => (Item: u, Passes: TimedPasses(u).ToList())).ToList();
        var passes = timed.SelectMany(t => t.Passes).ToList();

        return new TimeSpent(
            passes.Where(p => p.IsMeasured).Sum(p => p.Minutes),
            passes.Where(p => !p.IsMeasured).Sum(p => p.Minutes),
            timed.Count(t => t.Passes.Count > 0),
            timed.Count(t => t.Passes.Count == 0 && t.Item.Entries.Count > 0),
            passes
                .GroupBy(p => (p.Type, p.Year))
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Type)
                .Select(g => new TimeBucket(g.Key.Type, g.Key.Year, g.Sum(p => p.Minutes)))
                .ToList());
    }

    private static IEnumerable<PassTime> TimedPasses(UserMediaItem item) => item.Entries
        .Select(e => TimeOf(item, e))
        .OfType<PassTime>();

    private static PassTime? TimeOf(UserMediaItem item, ConsumptionEntry entry)
    {
        var media = item.MediaItem!;
        if (EffortMath.UnitsSpent(media, entry) is not { } units
            || EffortMath.ToMinutes(media, units) is not { } minutes)
            return null;

        return new PassTime(media.MediaType, PassYear(item, entry), minutes, entry.Effort is not null);
    }

    private sealed record PassTime(MediaType Type, int Year, double Minutes, bool IsMeasured);

    // Whole, in the year it closed: spreading undated estimates would invent a pace.
    private static int PassYear(UserMediaItem item, ConsumptionEntry entry) =>
        (entry.EndDate ?? entry.StartDate ?? item.AddedDate).Year;

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
        .Select(g => new UniverseCard(
            g.Key,
            g.Count(),
            g.Average(u => u.Rating),
            UniverseEffortOf(g),
            UniverseCovers(g)))
        .OrderByDescending(c => c.Works)
        .ToList();

    private static readonly (MediaBucket Bucket, string Unit)[] UniverseUnits =
        [(MediaBucket.Gaming, "h played"), (MediaBucket.Viewing, "h watched"), (MediaBucket.Reading, "pages read")];

    private static List<UniverseEffort> UniverseEffortOf(IEnumerable<UserMediaItem> works)
    {
        var spent = works
            .Select(u => (Bucket: MediaBuckets.Of(u.MediaItem!.MediaType), Amount: AmountSpent(u)))
            .ToList();

        return UniverseUnits
            .Select(b => (b.Bucket, b.Unit, Total: spent.Where(s => s.Bucket == b.Bucket).Sum(s => s.Amount)))
            .Where(b => b.Total > 0)
            .Select(b => new UniverseEffort(b.Bucket, MediaBuckets.Rounded(b.Bucket, b.Total), b.Unit))
            .ToList();
    }

    private static double AmountSpent(UserMediaItem item)
    {
        var media = item.MediaItem!;
        return MediaBuckets.Amount(media, item.Entries.Sum(e => EffortMath.UnitsSpent(media, e) ?? 0));
    }

    private static List<UniverseCover> UniverseCovers(IEnumerable<UserMediaItem> works) => works
        .OrderBy(u => u.Entries.Count == 0)
        .ThenBy(u => u.Entries.Select(e => e.StartDate).Min() ?? DateOnly.MaxValue)
        .Select(u => new UniverseCover(u.Id, u.MediaItem!.Title,
            u.MediaItem.LocalImagePath ?? u.MediaItem.ImageUrl, u.Status))
        .ToList();

    // Primary credit only, or one film trilogy floods the list with its screenwriters.
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

    private static MonthRecord? BuildBusiestMonth(List<UserMediaItem> items) => items
        .SelectMany(u => u.Entries)
        .SelectMany(LogDates)
        .GroupBy(d => (d.Year, d.Month))
        .Select(g => new MonthRecord(g.Key.Year, g.Key.Month, g.Count()))
        .OrderByDescending(m => m.Logs)
        .FirstOrDefault();

    private static IEnumerable<DateOnly> LogDates(ConsumptionEntry entry)
    {
        if (entry.StartDate is { } start) yield return start;
        if (entry.EndDate is { } end) yield return end;
        foreach (var step in EffortMath.Walk(entry).Where(s => s.Note.Kind == NoteKind.Progress))
            yield return DateOnly.FromDateTime(step.When);
    }
}
