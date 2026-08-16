using System.Linq.Expressions;
using MediaArchive.Data;
using MediaArchive.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services;

public record CoverCard(int UserMediaItemId, string Title, string? ImageUrl);

public record OpenPassSummary(int EntryId, DateOnly? StartDate, int? Effort, double? Progress);

public record ItemDetail(
    int UserMediaItemId,
    int MediaItemId,
    string Title,
    MediaType MediaType,
    string? Creator,
    string? ImageUrl,
    string? Description,
    DateOnly? ReleaseDate,
    int? Length,
    double? ExternalRating,
    int? ExternalRatingCount,
    string? Universe,
    string? Series,
    int? SeriesPosition,
    List<string> Genres,
    List<TagInput> Tags,
    MediaStatus Status,
    int? Rating,
    bool IsFavorite,
    DiscoverySource? Discovery,
    DateOnly AddedDate,
    OpenPassSummary? OpenPass,
    int PassCount);

public record PassNote(DateTime CreatedAt, NoteKind Kind, int? EffortAtTime, string? Text);

public record PassSummary(
    int EntryId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    PassOutcome? Outcome,
    int? RatingAtTime,
    int? Effort,
    ConsumptionContext? Context,
    List<PassNote> Notes);

// Reads shared across surfaces (Home, Library, Explore, item page).
public class CommonQueries(IDbContextFactory<AppDbContext> dbContextFactory)
{
    // Reusable projection: surface queries drop this into their own .Select.
    public static readonly Expression<Func<UserMediaItem, CoverCard>> ToCoverCard =
        u => new CoverCard(u.Id, u.MediaItem!.Title,
            u.MediaItem.LocalImagePath ?? u.MediaItem.ImageUrl);

    public async Task<List<CoverCard>> GetBacklogAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        return await db.UserMediaItems
            .Where(u => u.Status == MediaStatus.Interested)
            .OrderBy(u => u.AddedDate)
            .Select(ToCoverCard)
            .ToListAsync(ct);
    }

    public async Task<ItemDetail?> GetItemDetailAsync(int userMediaItemId,
        CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var item = await db.UserMediaItems
            .Where(u => u.Id == userMediaItemId)
            .Include(u => u.MediaItem).ThenInclude(m => m!.Genres).ThenInclude(mg => mg.Genre)
            .Include(u => u.MediaItem).ThenInclude(m => m!.Tags).ThenInclude(mt => mt.Tag)
            .Include(u => u.MediaItem).ThenInclude(m => m!.Credits).ThenInclude(mc => mc.Person)
            .Include(u => u.MediaItem).ThenInclude(m => m!.Series)
            .Include(u => u.MediaItem).ThenInclude(m => m!.Universe)
            .Include(u => u.Entries)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (item is null)
            return null;

        var media = item.MediaItem!;
        var open = item.Entries.FirstOrDefault(e => e.EndDate is null);

        var openPass = open is null
            ? null
            : new OpenPassSummary(open.Id, open.StartDate, open.Effort,
                open.Effort is { } effort && media.Length is { } length
                    ? (double)effort / length * 100
                    : null);

        return new ItemDetail(
            item.Id,
            media.Id,
            media.Title,
            media.MediaType,
            media.Creator,
            media.LocalImagePath ?? media.ImageUrl,
            media.Description,
            media.ReleaseDate,
            media.Length,
            media.ExternalRating,
            media.ExternalRatingCount,
            media.Universe?.Name,
            media.Series is not null && media.Series.Id != Series.StandaloneId
                ? media.Series.Name
                : null,
            media.SeriesPosition,
            media.Genres.Where(mg => mg.Genre is not null)
                .Select(mg => mg.Genre!.Name).Order().ToList(),
            media.Tags.Where(mt => mt.Tag is not null)
                .Select(mt => new TagInput(mt.Tag!.Name, mt.Tag.Facet, mt.Tag.AppliesTo))
                .OrderBy(t => t.Name)
                .ToList(),
            item.Status,
            item.Rating,
            item.IsFavorite,
            item.Discovery,
            item.AddedDate,
            openPass,
            item.Entries.Count);
    }

    public async Task<List<PassSummary>> GetPassHistoryAsync(int userMediaItemId,
        CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var entries = await db.ConsumptionEntries
            .Where(e => e.UserMediaItemId == userMediaItemId)
            .Include(e => e.Notes)
            .OrderByDescending(e => e.StartDate)
            .ThenByDescending(e => e.Id)
            .AsNoTracking()
            .ToListAsync(ct);

        return entries
            .Select(e => new PassSummary(
                e.Id, e.StartDate, e.EndDate, e.Outcome, e.RatingAtTime, e.Effort, e.Context,
                e.Notes
                    .OrderBy(n => n.CreatedAt)
                    .Select(n => new PassNote(n.CreatedAt, n.Kind, n.EffortAtTime, n.Text))
                    .ToList()))
            .ToList();
    }
}
