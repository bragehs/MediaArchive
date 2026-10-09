using System.Linq.Expressions;
using MediaArchive.Data;
using MediaArchive.Models;
using MediaArchive.Services.Providers;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services.Import;

public static class VocabularyResolver
{
    public static Task AddWorkDetailsAsync(AppDbContext db, MediaItem mediaItem,
        WorkDetails details, CancellationToken ct = default)
    {
        return ApplyWorkDetailsAsync(new Target(db, mediaItem, Replace: false), details, ct);
    }

    public static Task ReplaceWorkDetailsAsync(AppDbContext db, MediaItem mediaItem,
        WorkDetails details, CancellationToken ct = default)
    {
        return ApplyWorkDetailsAsync(new Target(db, mediaItem, Replace: true), details, ct);
    }

    private static async Task ApplyWorkDetailsAsync(Target target, WorkDetails details,
        CancellationToken ct)
    {
        var (db, mediaItem, replace) = target;

        var universes = await ResolveNamedAsync(db, [details.Universe],
            name => new Universe { Name = name }, ct);

        if (universes.Count > 0)
        {
            mediaItem.Universe = universes[0];
        }
        else if (replace)
        {
            mediaItem.Universe = null;
            mediaItem.UniverseId = null;
        }

        var series = await ResolveSeriesAsync(db, details.Series, ct);
        mediaItem.Series = series;
        mediaItem.SeriesPosition = series.Id == Series.StandaloneId ? null : details.SeriesPosition;

        await ApplyGenresAsync(target, details.Genres, ct);
        await ApplyTagsAsync(target, details.Tags, ct);
    }

    public static string NormaliseTerm(string name) => name.Trim().ToLowerInvariant();

    private static async Task ApplyGenresAsync(Target target, List<string> names, CancellationToken ct)
    {
        var (db, mediaItem, replace) = target;

        var genres = await ResolveNamedAsync(db,
            names.Select(n => string.IsNullOrWhiteSpace(n) ? n : NormaliseTerm(n)),
            name => new Genre { Name = name }, ct);
        if (genres.Count == 0 && !replace)
            return;

        await LoadIfPersistedAsync(db, mediaItem, m => m.Genres, ct);

        if (replace)
        {
            var wanted = genres.Where(g => g.Id != 0).Select(g => g.Id).ToHashSet();
            mediaItem.Genres.RemoveAll(mg => !wanted.Contains(mg.GenreId));
        }

        var linked = mediaItem.Genres.Select(mg => mg.GenreId).ToHashSet();

        foreach (var genre in genres.Where(g => g.Id == 0 || !linked.Contains(g.Id)))
            mediaItem.Genres.Add(new MediaItemGenre { Genre = genre });
    }

    private static async Task ApplyTagsAsync(Target target, List<TagInput> inputs, CancellationToken ct)
    {
        var (db, mediaItem, replace) = target;

        var classification = inputs
            .Where(i => !string.IsNullOrWhiteSpace(i.Name))
            .GroupBy(i => i.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var tags = await ResolveNamedAsync(db,
            inputs.Select(i => string.IsNullOrWhiteSpace(i.Name) ? i.Name : NormaliseTerm(i.Name)),
            name => new Tag
            {
                Name = name,
                Facet = classification[name].Facet,
                AppliesTo = classification[name].AppliesTo
            }, ct);

        if (tags.Count == 0 && !replace)
            return;

        await LoadIfPersistedAsync(db, mediaItem, m => m.Tags, ct);

        if (replace)
        {
            var wanted = tags.Where(t => t.Id != 0).Select(t => t.Id).ToHashSet();
            mediaItem.Tags.RemoveAll(mt => !wanted.Contains(mt.TagId));
        }

        var linked = mediaItem.Tags.Select(mt => mt.TagId).ToHashSet();

        foreach (var tag in tags.Where(t => t.Id == 0 || !linked.Contains(t.Id)))
            mediaItem.Tags.Add(new MediaItemTag { Tag = tag });
    }

    public static async Task ApplyCreditsAsync(AppDbContext db, MediaItem mediaItem,
        List<CreditDto> credits, CancellationToken ct = default)
    {
        credits = DistinctCredits(credits);

        if (credits.Count == 0)
            return;

        var people = await ResolveNamedAsync(db, credits.Select(c => c.Name),
            name => new Person { Name = name }, ct);

        if (people.Count == 0)
            return;

        await LoadIfPersistedAsync(db, mediaItem, m => m.Credits, ct);

        LinkCredits(mediaItem, credits, people);
    }

    // (Person, Role) is the composite key, so a repeated pair would collide.
    private static List<CreditDto> DistinctCredits(List<CreditDto> credits)
    {
        return credits
            .Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .DistinctBy(c => (c.Name.Trim().ToLowerInvariant(), c.Role))
            .ToList();
    }

    private static void LinkCredits(MediaItem mediaItem, List<CreditDto> credits, List<Person> people)
    {
        var byName = people.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
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

    public static async Task<Series> ResolveSeriesAsync(AppDbContext db, string? name,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            name.Trim().Equals(Series.StandaloneName, StringComparison.OrdinalIgnoreCase))
            return await db.Series.FirstAsync(s => s.Id == Series.StandaloneId, ct);

        var resolved = await ResolveNamedAsync(db, [name], n => new Series { Name = n }, ct);
        return resolved[0];
    }

    public static async Task<List<T>> ResolveNamedAsync<T>(AppDbContext db,
        IEnumerable<string?> names, Func<string, T> create, CancellationToken ct = default)
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

        // EF.Property keeps this translatable: the provider can't see through the generic INamed member access.
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

    private static async Task LoadIfPersistedAsync<TProp>(AppDbContext db, MediaItem mediaItem,
        Expression<Func<MediaItem, IEnumerable<TProp>>> collection, CancellationToken ct)
        where TProp : class
    {
        if (mediaItem.Id != 0)
            await db.Entry(mediaItem).Collection(collection).LoadAsync(ct);
    }

    private sealed record Target(AppDbContext Db, MediaItem Item, bool Replace);
}