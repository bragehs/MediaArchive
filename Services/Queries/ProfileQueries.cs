using MediaArchive.Data;
using MediaArchive.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services;

// A pass clipped to one calendar year. Positions are in days, not percentages —
// turning days into CSS widths is the view's job.
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

// Kept as the enum, not a pre-formatted label: the view already owns type
// labels and colours via UiHelpers.
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
    // Stripes closer than this share no lane, so two short passes a few days
    // apart stay visually separate once widths are floored to a few pixels.
    private const int LaneGapDays = 3;

    // One materialise, then every aggregate in memory. The archive is a single
    // local user's few hundred rows, so one round trip beats a SQL aggregate per
    // panel — and the year ledger walks consumption intervals, which is C# work
    // either way.
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

        // GenreId lives on the join entity, so counting the vocabulary never
        // needs the Genre rows themselves loaded.
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
            // An item counts once per genre it carries, so these sum to more
            // than the archive total — a genre row is "items tagged this", not
            // a partition of the library.
            ByGenre: items
                .SelectMany(u => u.MediaItem!.Genres)
                .Where(mg => mg.Genre is not null)
                .GroupBy(mg => mg.Genre!.Name)
                .Select(g => new GenreCount(g.Key, g.Count()))
                .OrderByDescending(g => g.Count)
                .ThenBy(g => g.Name)
                .ToList());
    }

    // One band per calendar year, newest first. A pass that spans New Year is
    // drawn in every year it touches, clipped to each — the band answers "what
    // was running during this year", so dropping the carried-over part would
    // leave months looking idle that weren't.
    private static List<YearActivity> BuildYears(List<UserMediaItem> items, DateOnly today)
    {
        var passes = items
            .SelectMany(u => u.Entries, (u, e) => (Item: u, Entry: e))
            .Where(p => p.Entry.StartDate is not null)
            .Select(p => (
                p.Item,
                Start: p.Entry.StartDate!.Value,
                // An unfinished pass runs to today and never past it.
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

            // Coverage sweep. Peak depth is true concurrency; the non-zero count
            // is the union of the intervals. Summing pass lengths instead would
            // double-count every day two things were open.
            var cover = new int[daysInYear];
            foreach (var p in inYear)
                for (var d = p.StartDay; d < p.StartDay + p.Days; d++)
                    cover[d]++;

            // Greedy lane packing, purely so stripes don't overlap. LaneGapDays
            // makes LaneCount drift above MaxConcurrent, so it drives the band's
            // height and never the "deep at its busiest" figure.
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
