using MediaArchive.Models;
using MediaArchive.Services.Providers;

namespace MediaArchive.Tests;

// Fast, offline, deterministic. Feeds a captured Open Library response through
// the provider's REAL parsing/mapping via a fake handler. No [Trait] tag, so it
// runs on every `dotnet test`.
public class OpenLibraryProviderTests
{
    private static OpenLibraryProvider ProviderReturning(string json, out FakeHttpMessageHandler handler)
    {
        handler = new FakeHttpMessageHandler(json);
        return new OpenLibraryProvider(new HttpClient(handler));
    }

    // GetByIdAsync fans out to two endpoints, so route by URL fragment.
    private static OpenLibraryProvider ProviderReturning(
        string searchJson, string workJson, out FakeHttpMessageHandler handler)
    {
        handler = new FakeHttpMessageHandler([("search.json", searchJson), ("/works/", workJson)]);
        return new OpenLibraryProvider(new HttpClient(handler));
    }

    private static async Task<string> LoadFixtureAsync(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        return await File.ReadAllTextAsync(path);
    }

    [Fact]
    public async Task SearchAsync_MapsAllDocs_FromCapturedResponse()
    {
        var json = await LoadFixtureAsync("open-library-dune.json");
        var provider = ProviderReturning(json, out _);

        var results = await provider.SearchAsync("dune", MediaType.Book);

        Assert.Equal(3, results.Count);
        Assert.All(results, r =>
        {
            Assert.Equal("OpenLibrary", r.ExternalSource);
            Assert.Equal(MediaType.Book, r.MediaType);
        });
    }

    [Fact]
    public async Task SearchAsync_MapsCoreFields_ForFirstDoc()
    {
        var json = await LoadFixtureAsync("open-library-dune.json");
        var provider = ProviderReturning(json, out _);

        var dune = (await provider.SearchAsync("dune", MediaType.Book))[0];

        // The API key is "/works/OL893415W"; the app stores the bare work id.
        Assert.Equal("OL893415W", dune.ExternalId);
        Assert.Equal("Dune", dune.Title);
        Assert.Equal(1965, dune.ReleaseYear);
        Assert.Equal("https://covers.openlibrary.org/b/id/11481354-M.jpg", dune.ImageUrl);
    }

    [Fact]
    public async Task SearchAsync_TakesYearFromFirstPublishYear()
    {
        var json = await LoadFixtureAsync("open-library-dune.json");
        var provider = ProviderReturning(json, out _);

        var messiah = (await provider.SearchAsync("dune", MediaType.Book))[1];

        Assert.Equal(1969, messiah.ReleaseYear);
    }

    [Fact]
    public async Task SearchAsync_HandlesMissingCoverAndYear()
    {
        var json = await LoadFixtureAsync("open-library-dune.json");
        var provider = ProviderReturning(json, out _);

        // Third doc has no cover_i and no first_publish_year.
        var sparse = (await provider.SearchAsync("dune", MediaType.Book))[2];

        Assert.Null(sparse.ReleaseYear);
        Assert.Null(sparse.ImageUrl);
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmpty_WhenNoDocs()
    {
        var provider = ProviderReturning("""{ "numFound": 0, "docs": [] }""", out _);

        var results = await provider.SearchAsync("zzzznotarealbook", MediaType.Book);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_UrlEncodesTheQuery()
    {
        var provider = ProviderReturning("""{ "docs": [] }""", out var handler);

        await provider.SearchAsync("ender's game", MediaType.Book);

        // The space and apostrophe must be percent-encoded in the outgoing URL.
        Assert.Contains("ender%27s%20game", handler.LastRequestUri!.Query);
    }

    [Fact]
    public async Task SearchAsync_RequestsOnlyTheFieldsTheDtoNeeds()
    {
        var provider = ProviderReturning("""{ "docs": [] }""", out var handler);

        await provider.SearchAsync("dune", MediaType.Book);

        var query = Uri.UnescapeDataString(handler.LastRequestUri!.Query);
        Assert.Contains("fields=key,title,author_name", query);
        Assert.Contains("ratings_average", query);
        Assert.Contains("limit=5", query);
    }

    [Fact]
    public async Task GetByIdAsync_CombinesSearchRowAndWorkRecord()
    {
        var searchJson = await LoadFixtureAsync("open-library-dune.json");
        var workJson = await LoadFixtureAsync("open-library-dune-work.json");
        var provider = ProviderReturning(searchJson, workJson, out _);

        var item = await provider.GetByIdAsync("OL893415W", MediaType.Book);

        Assert.NotNull(item);
        Assert.Equal("OpenLibrary", item.ExternalSource);
        Assert.Equal("OL893415W", item.ExternalId);
        Assert.Equal("Dune", item.Title);
        Assert.Equal(1965, item.ReleaseYear);
        // number_of_pages_median from the search row.
        Assert.Equal(592, item.Length);
        // Detail view asks for the large cover.
        Assert.Equal("https://covers.openlibrary.org/b/id/11481354-L.jpg", item.ImageUrl);
        // description only exists on the work record.
        Assert.NotNull(item.Description);
        Assert.Contains("Arrakis", item.Description);
    }

    [Fact]
    public async Task GetByIdAsync_MapsAuthorsAsCredits()
    {
        var searchJson = await LoadFixtureAsync("open-library-dune.json");
        var workJson = await LoadFixtureAsync("open-library-dune-work.json");
        var provider = ProviderReturning(searchJson, workJson, out _);

        var item = await provider.GetByIdAsync("OL893415W", MediaType.Book);

        Assert.NotNull(item);
        Assert.Contains(item.Credits, c => c is { Name: "Frank Herbert", Role: CreditRole.Author });
        Assert.All(item.Credits, c => Assert.Equal(CreditRole.Author, c.Role));
        Assert.Equal("Frank Herbert", item.Creator);
    }

    [Fact]
    public async Task GetByIdAsync_DropsMachineTagsFromSubjects()
    {
        var searchJson = await LoadFixtureAsync("open-library-dune.json");
        var workJson = await LoadFixtureAsync("open-library-dune-work.json");
        var provider = ProviderReturning(searchJson, workJson, out _);

        var item = await provider.GetByIdAsync("OL893415W", MediaType.Book);

        Assert.NotNull(item);
        Assert.Contains("Science fiction", item.Genres);
        // "award:hugo_award=1966" and friends are indexing artefacts, not genres.
        Assert.All(item.Genres, g =>
        {
            Assert.DoesNotContain(':', g!);
            Assert.DoesNotContain('=', g!);
        });
        Assert.True(item.Genres.Count <= 8);
    }

    [Fact]
    public async Task GetByIdAsync_NormalisesRatingToFivePointScale()
    {
        var searchJson = await LoadFixtureAsync("open-library-dune.json");
        var workJson = await LoadFixtureAsync("open-library-dune-work.json");
        var provider = ProviderReturning(searchJson, workJson, out _);

        var item = await provider.GetByIdAsync("OL893415W", MediaType.Book);

        Assert.NotNull(item);
        Assert.NotNull(item.ExternalRating);
        Assert.InRange(item.ExternalRating.Value, 0, RatingScale.Max);
        Assert.NotNull(item.ExternalRatingCount);
    }

    [Fact]
    public async Task GetByIdAsync_AcceptsAFullWorkKey()
    {
        var searchJson = await LoadFixtureAsync("open-library-dune.json");
        var workJson = await LoadFixtureAsync("open-library-dune-work.json");
        var provider = ProviderReturning(searchJson, workJson, out var handler);

        var item = await provider.GetByIdAsync("/works/OL893415W", MediaType.Book);

        Assert.NotNull(item);
        Assert.Equal("OL893415W", item.ExternalId);
        // The work request must not end up as /works//works/OL893415W.json
        Assert.Contains(handler.RequestUris, u => u.AbsolutePath == "/works/OL893415W.json");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNeitherEndpointHasTheWork()
    {
        var handler = new FakeHttpMessageHandler([("search.json", """{ "docs": [] }""")]);
        var provider = new OpenLibraryProvider(new HttpClient(handler));

        // No "/works/" route registered, so the work request 404s.
        var item = await provider.GetByIdAsync("OL0000000W", MediaType.Book);

        Assert.Null(item);
    }

    [Fact]
    public async Task GetByIdAsync_ReadsDescription_WhenItIsATextObject()
    {
        const string workJson = """
                                {
                                  "title": "Dune",
                                  "description": { "type": "/type/text", "value": "A desert planet." }
                                }
                                """;
        var provider = ProviderReturning("""{ "docs": [] }""", workJson, out _);

        var item = await provider.GetByIdAsync("OL893415W", MediaType.Book);

        Assert.NotNull(item);
        Assert.Equal("A desert planet.", item.Description);
    }

    [Fact]
    public async Task GetByIdAsync_StripsMarkdownAndSourceFooter_FromDescription()
    {
        const string workJson = """
                                {
                                  "title": "Dune",
                                  "description": "A [desert planet](https://example.com/arrakis).\r\n\r\n----------\r\n\r\n[source][1]\n\n[1]: https://example.com"
                                }
                                """;
        var provider = ProviderReturning("""{ "docs": [] }""", workJson, out _);

        var item = await provider.GetByIdAsync("OL893415W", MediaType.Book);

        Assert.NotNull(item);
        Assert.Equal("A desert planet.", item.Description);
    }

    [Fact]
    public async Task GetByIdAsync_FallsBackToTheWorkRecord_WhenNotInTheSearchIndex()
    {
        const string workJson = """
                                {
                                  "title": "An Unindexed Work",
                                  "covers": [-1, 12345],
                                  "subjects": ["Fiction", "award:hugo_award=1966"]
                                }
                                """;
        var provider = ProviderReturning("""{ "docs": [] }""", workJson, out _);

        var item = await provider.GetByIdAsync("OL999W", MediaType.Book);

        Assert.NotNull(item);
        Assert.Equal("An Unindexed Work", item.Title);
        // -1 is Open Library's placeholder for a deleted cover.
        Assert.Equal("https://covers.openlibrary.org/b/id/12345-L.jpg", item.ImageUrl);
        Assert.Equal(["Fiction"], item.Genres);
        Assert.Empty(item.Credits);
        Assert.Null(item.ExternalRating);
    }

    [Fact]
    public void CanHandle_OnlyBooks()
    {
        var provider = ProviderReturning("""{ "docs": [] }""", out _);

        Assert.True(provider.CanHandle(MediaType.Book));
        Assert.False(provider.CanHandle(MediaType.Movie));
        Assert.False(provider.CanHandle(MediaType.Game));
    }
}
