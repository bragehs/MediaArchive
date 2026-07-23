using System.Linq.Expressions;
using MediaArchive.Data;
using MediaArchive.Models;
using MediaArchive.Services.Providers;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services;

// The controlled vocabulary as it currently exists, for pick-or-create inputs.
public record Vocabulary(
    List<string> Genres,
    List<string> Tags,
    List<string> Universes,
    List<string> Series);

public class MediaLibraryService(IDbContextFactory<AppDbContext> dbContextFactory)
{
    public async Task<Vocabulary> GetVocabularyAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        // Single-user archive: these lists stay small enough to load whole.
        var genres = await db.Genres.OrderBy(g => g.Name).Select(g => g.Name).ToListAsync(ct);
        var tags = await db.Tags.OrderBy(t => t.Name).Select(t => t.Name).ToListAsync(ct);
        var universes = await db.Universes.OrderBy(u => u.Name).Select(u => u.Name).ToListAsync(ct);
        var series = await db.Series.OrderBy(s => s.Name).Select(s => s.Name).ToListAsync(ct);

        return new Vocabulary(genres, tags, universes, series);
    }

    public async Task<int> AddToLibraryAsync(MediaItemDto item, WorkDetails details,
        CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var mediaItem = await ResolveMediaItemAsync(db, item, ct);

        var universes = await ResolveNamedAsync(db, [details.Universe],
            name => new Universe { Name = name }, ct);
        if (universes.Count > 0)
            mediaItem.Universe = universes[0];

        var series = await ResolveSeriesAsync(db, details.Series, ct);
        mediaItem.Series = series;
        // Position is meaningless without a real series.
        mediaItem.SeriesPosition = series.Id == Series.StandaloneId ? null : details.SeriesPosition;

        await ApplyGenresAsync(db, mediaItem, details.Genres, ct);
        await ApplyTagsAsync(db, mediaItem, details.Tags, ct);
        await ApplyCreditsAsync(db, mediaItem, item.Credits, ct);

        var userItem = await ResolveUserMediaItemAsync(db, mediaItem, ct);
        userItem.Discovery = details.Discovery ?? userItem.Discovery;

        await db.SaveChangesAsync(ct);

        return userItem.Id;
    }

    private static async Task<MediaItem> ResolveMediaItemAsync(AppDbContext db, MediaItemDto dto,
        CancellationToken ct)
    {
        var existing = await db.MediaItems
            .FirstOrDefaultAsync(
                m => m.ExternalSource == dto.ExternalSource && m.ExternalId == dto.ExternalId, ct);

        if (existing is not null)
            return existing;

        var created = MediaItemMapper.ToEntity(dto);

        db.MediaItems.Add(created);
        return created;
    }

    private static async Task<UserMediaItem> ResolveUserMediaItemAsync(AppDbContext db,
        MediaItem mediaItem, CancellationToken ct)
    {
        if (mediaItem.Id != 0)
        {
            var existing = await db.UserMediaItems
                .FirstOrDefaultAsync(u => u.MediaItemId == mediaItem.Id, ct);

            if (existing is not null)
                return existing;
        }

        var created = new UserMediaItem { MediaItem = mediaItem };
        db.UserMediaItems.Add(created);
        return created;
    }

    private static async Task ApplyGenresAsync(AppDbContext db, MediaItem mediaItem,
        List<string> names, CancellationToken ct)
    {
        var genres = await ResolveNamedAsync(db, names, name => new Genre { Name = name }, ct);
        if (genres.Count == 0)
            return;

        await LoadIfPersistedAsync(db, mediaItem, m => m.Genres, ct);

        var linked = mediaItem.Genres.Select(mg => mg.GenreId).ToHashSet();

        foreach (var genre in genres.Where(g => g.Id == 0 || !linked.Contains(g.Id)))
            mediaItem.Genres.Add(new MediaItemGenre { Genre = genre });
    }

    private static async Task ApplyCreditsAsync(AppDbContext db, MediaItem mediaItem,
        List<CreditDto> credits, CancellationToken ct)
    {
        // (Person, Role) is the composite key, so a repeated pair would collide.
        credits = credits
            .Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .DistinctBy(c => (c.Name.Trim().ToLowerInvariant(), c.Role))
            .ToList();

        if (credits.Count == 0)
            return;

        var people = await ResolveNamedAsync(db, credits.Select(c => c.Name),
            name => new Person { Name = name }, ct);

        if (people.Count == 0)
            return;

        var byName = people.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        await LoadIfPersistedAsync(db, mediaItem, m => m.Credits, ct);

        var linked = mediaItem.Credits.Select(mc => (mc.PersonId, mc.Role)).ToHashSet();

        foreach (var credit in credits)
        {
            if (!byName.TryGetValue(credit.Name.Trim(), out var person))
                continue;

            if (person.Id != 0 && !linked.Add((person.Id, credit.Role)))
                continue;

            mediaItem.Credits.Add(new MediaItemCredit { Person = person, Role = credit.Role });
        }
    }

    private static async Task ApplyTagsAsync(AppDbContext db, MediaItem mediaItem,
        List<TagInput> inputs, CancellationToken ct)
    {
        var classification = inputs
            .Where(i => !string.IsNullOrWhiteSpace(i.Name))
            .GroupBy(i => i.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var tags = await ResolveNamedAsync(db, inputs.Select(i => i.Name),
            name => new Tag
            {
                Name = name,
                Facet = classification[name].Facet,
                AppliesTo = classification[name].AppliesTo
            }, ct);

        if (tags.Count == 0)
            return;

        await LoadIfPersistedAsync(db, mediaItem, m => m.Tags, ct);

        var linked = mediaItem.Tags.Select(mt => mt.TagId).ToHashSet();

        foreach (var tag in tags.Where(t => t.Id == 0 || !linked.Contains(t.Id)))
            mediaItem.Tags.Add(new MediaItemTag { Tag = tag });
    }

    // A brand-new entity has nothing in the DB to load; an existing one needs its
    // join rows pulled in before we can diff against them.
    private static async Task LoadIfPersistedAsync<TProp>(AppDbContext db, MediaItem mediaItem,
        Expression<Func<MediaItem, IEnumerable<TProp>>> collection, CancellationToken ct)
        where TProp : class
    {
        if (mediaItem.Id != 0)
            await db.Entry(mediaItem).Collection(collection).LoadAsync(ct);
    }

    private static async Task<Series> ResolveSeriesAsync(AppDbContext db, string? name,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            name.Trim().Equals(Series.StandaloneName, StringComparison.OrdinalIgnoreCase))
            return await db.Series.FirstAsync(s => s.Id == Series.StandaloneId, ct);

        var resolved = await ResolveNamedAsync(db, [name], n => new Series { Name = n }, ct);
        return resolved[0];
    }

    private static async Task<List<T>> ResolveNamedAsync<T>(AppDbContext db,
        IEnumerable<string?> names, Func<string, T> create, CancellationToken ct)
        where T : class, INamed
    {
        var wanted = names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!.Trim())
            .GroupBy(n => n.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        if (wanted.Count == 0)
            return [];

        var set = db.Set<T>();
        var keys = wanted.Keys.ToList();

        // EF.Property keeps this translatable: T is generic here, so the provider
        // can't see through the INamed member access on its own.
        var existing = await set
            .Where(e => keys.Contains(EF.Property<string>(e, nameof(INamed.Name)).ToLower()))
            .ToListAsync(ct);

        var byKey = existing.ToDictionary(e => e.Name.ToLowerInvariant());
        var result = new List<T>(wanted.Count);

        foreach (var (key, display) in wanted)
        {
            if (!byKey.TryGetValue(key, out var match))
            {
                match = create(display);
                set.Add(match);
            }

            result.Add(match);
        }

        return result;
    }
}