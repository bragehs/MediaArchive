using MediaArchive.Data;
using MediaArchive.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services;

public class LibraryService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<List<UserMediaItem>> GetLibraryAsync(MediaStatus? status = null, MediaType? type = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.UserMediaItems
            .Include(u => u.MediaItem)!.ThenInclude(m => m!.Genres).ThenInclude(g => g.Genre)
            .AsQueryable();

        if (status is { } s) q = q.Where(u => u.Status == s);
        if (type is { } t) q = q.Where(u => u.MediaItem!.MediaType == t);

        return await q.OrderByDescending(u => u.AddedDate).ToListAsync();
    }

    public async Task<UserMediaItem?> GetDetailAsync(int mediaItemId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.UserMediaItems
            .Include(u => u.MediaItem)!.ThenInclude(m => m!.Genres).ThenInclude(g => g.Genre)
            .Include(u => u.MediaItem)!.ThenInclude(m => m!.Universe)
            .Include(u => u.Entries)
            .FirstOrDefaultAsync(u => u.MediaItemId == mediaItemId);
    }

    public async Task<List<UserMediaItem>> GetCurrentlyConsumingAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.UserMediaItems
            .Include(u => u.MediaItem)
            .Where(u => u.Status == MediaStatus.InProgress)
            .OrderByDescending(u => u.AddedDate)
            .ToListAsync();
    }

    public async Task<List<ConsumptionEntry>> GetDiaryAsync(int take = 100)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ConsumptionEntries
            .Include(e => e.UserMediaItem)!.ThenInclude(u => u!.MediaItem)
            .OrderByDescending(e => e.EndDate ?? e.StartDate)
            .Take(take)
            .ToListAsync();
    }

    public async Task<ProfileStats> GetProfileAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var items = await db.UserMediaItems
            .Include(u => u.MediaItem)!.ThenInclude(m => m!.Genres).ThenInclude(g => g.Genre)
            .ToListAsync();

        var rated = items.Where(i => i.Rating is not null).ToList();

        return new ProfileStats
        {
            TotalItems = items.Count,
            Completed = items.Count(i => i.Status == MediaStatus.Completed),
            InProgress = items.Count(i => i.Status == MediaStatus.InProgress),
            Favorites = items.Count(i => i.IsFavorite),
            AverageRating = rated.Count > 0 ? Math.Round(rated.Average(i => i.Rating!.Value), 1) : 0,
            CountByType = items
                .GroupBy(i => i.MediaItem!.MediaType)
                .ToDictionary(g => g.Key, g => g.Count()),
            TopGenres = items
                .SelectMany(i => i.MediaItem!.Genres.Select(g => g.Genre!.Name))
                .GroupBy(n => n)
                .Select(g => new GenreCount(g.Key, g.Count()))
                .OrderByDescending(g => g.Count)
                .Take(8)
                .ToList(),
            HighestRated = rated
                .OrderByDescending(i => i.Rating)
                .Take(6)
                .ToList()
        };
    }
}

public class ProfileStats
{
    public int TotalItems { get; init; }
    public int Completed { get; init; }
    public int InProgress { get; init; }
    public int Favorites { get; init; }
    public double AverageRating { get; init; }
    public Dictionary<MediaType, int> CountByType { get; init; } = [];
    public List<GenreCount> TopGenres { get; init; } = [];
    public List<UserMediaItem> HighestRated { get; init; } = [];
}

public record GenreCount(string Name, int Count);
