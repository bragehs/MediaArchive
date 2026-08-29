using MediaArchive.Data;
using MediaArchive.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services;

// A pass clipped to one calendar year, positioned in days.
public record YearPass(
    int UserMediaItemId,
    string Title,
    MediaType MediaType,
    int StartDay,
    int Days,
    bool CarriedIn,
    bool CarriedOut,
    bool StillRunning,
    int Lane);

public record YearActivity(
    int Year,
    int DaysInYear,
    int PassCount,
    int MaxConcurrent,
    int DaysRunning,
    int LaneCount,
    IReadOnlyList<YearPass> Passes);

public record TypeCount(MediaType MediaType, int Count);

public record GenreCount(string Name, int Count);

public record ProfileSnapshot(
    int ItemsLogged,
    double? AvgRating,
    int RatedCount,
    int Finished,
    int GenreCount,
    IReadOnlyList<YearActivity> Years,
    IReadOnlyList<TypeCount> ByType,
    IReadOnlyList<GenreCount> ByGenre);

public class ProfileQueries(IDbContextFactory<AppDbContext> dbContextFactory)
{
    // Passes closer than this share no lane, so short stripes stay separate.
    private const int LaneGapDays = 3;

    // A few hundred rows total: one round trip, every aggregate in memory.
    public async Task<ProfileSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var items = await db.UserMediaItems
            .Include(u => u.MediaItem).ThenInclude(m => m!.Genres).ThenInclude(mg => mg.Genre)
            .Include(u => u.Entries)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(ct);

        var ratings = items
            .Where(u => u.Rating is not null)
            .Select(u => u.Rating!.Value)
            .ToList();

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
            Years: BuildYears(items, DateOnly.FromDateTime(DateTime.Today)),
            ByType: items
                .GroupBy(u => u.MediaItem!.MediaType)
                .Select(g => new TypeCount(g.Key, g.Count()))
                .OrderByDescending(t => t.Count)
                .ThenBy(t => t.MediaType)
                .ToList(),
            // An item counts once per genre, so rows sum past the archive total.
            ByGenre: items
                .SelectMany(u => u.MediaItem!.Genres)
                .Where(mg => mg.Genre is not null)
                .GroupBy(mg => mg.Genre!.Name)
                .Select(g => new GenreCount(g.Key, g.Count()))
                .OrderByDescending(g => g.Count)
                .ThenBy(g => g.Name)
                .ToList());
    }

    // A pass spanning New Year is drawn in every year it touches, clipped to each.
    private static List<YearActivity> BuildYears(List<UserMediaItem> items, DateOnly today)
    {
        var passes = items
            .SelectMany(u => u.Entries, (u, e) => (Item: u, Entry: e))
            .Where(p => p.Entry.StartDate is not null)
            .Select(p => (
                p.Item,
                Start: p.Entry.StartDate!.Value,
                End: p.Entry.EndDate ?? today,
                IsOpen: p.Entry.EndDate is null))
            .Where(p => p.End >= p.Start)
            .ToList();

        var years = new List<YearActivity>();
        if (passes.Count == 0) return years;

        for (var year = passes.Max(p => p.End.Year); year >= passes.Min(p => p.Start.Year); year--)
        {
            var jan1 = new DateOnly(year, 1, 1);
            var dec31 = new DateOnly(year, 12, 31);
            var daysInYear = dec31.DayNumber - jan1.DayNumber + 1;

            var inYear = passes
                .Where(p => p.Start <= dec31 && p.End >= jan1)
                .Select(p =>
                {
                    var from = p.Start > jan1 ? p.Start : jan1;
                    var to = p.End < dec31 ? p.End : dec31;
                    return (
                        p.Item,
                        StartDay: from.DayNumber - jan1.DayNumber,
                        Days: to.DayNumber - from.DayNumber + 1,
                        CarriedIn: p.Start < jan1,
                        CarriedOut: p.End > dec31,
                        StillRunning: p.IsOpen && to == p.End);
                })
                .OrderBy(p => p.StartDay)
                .ThenByDescending(p => p.Days)
                .ToList();

            if (inYear.Count == 0) continue;

            // Coverage sweep: peak depth is concurrency, non-zero days the union.
            var cover = new int[daysInYear];
            foreach (var p in inYear)
                for (var d = p.StartDay; d < p.StartDay + p.Days; d++)
                    cover[d]++;

            // Greedy lane packing; LaneGapDays can push LaneCount above
            // MaxConcurrent, so it drives band height only.
            var laneEnds = new List<int>();
            var laid = new List<YearPass>(inYear.Count);
            foreach (var p in inYear)
            {
                var lane = laneEnds.FindIndex(end => end < p.StartDay - LaneGapDays);
                if (lane < 0)
                {
                    lane = laneEnds.Count;
                    laneEnds.Add(0);
                }
                laneEnds[lane] = p.StartDay + p.Days - 1;

                var media = p.Item.MediaItem!;
                laid.Add(new YearPass(
                    p.Item.Id, media.Title, media.MediaType,
                    p.StartDay, p.Days,
                    p.CarriedIn, p.CarriedOut, p.StillRunning, lane));
            }

            years.Add(new YearActivity(
                Year: year,
                DaysInYear: daysInYear,
                PassCount: inYear.Count,
                MaxConcurrent: cover.Max(),
                DaysRunning: cover.Count(c => c > 0),
                LaneCount: laneEnds.Count,
                Passes: laid));
        }

        return years;
    }
}
