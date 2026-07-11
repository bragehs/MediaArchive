using MediaArchive.Models;
using MediaArchive.Services.Providers;
using Microsoft.Extensions.Options;

namespace MediaArchive.Tests;

// Fast, offline, deterministic. Feeds a captured Google Books response through
// the provider's REAL parsing/mapping via a fake handler. No [Trait] tag, so it
// runs on every `dotnet test`.
public class GoogleBooksProviderTests
{
    private static GoogleBooksProvider ProviderReturning(
        string json, out FakeHttpMessageHandler handler, string? apiKey = null)
    {
        handler = new FakeHttpMessageHandler(json);
        var http = new HttpClient(handler);
        var options = Options.Create(new GoogleBooksOptions { ApiKey = apiKey });
        return new GoogleBooksProvider(http, options);
    }

    private static async Task<string> LoadFixtureAsync(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        return await File.ReadAllTextAsync(path);
    }

    [Fact]
    public async Task SearchAsync_MapsAllVolumes_FromCapturedResponse()
    {
        var json = await LoadFixtureAsync("google-books-dune.json");
        var provider = ProviderReturning(json, out _);

        var results = await provider.SearchAsync("dune", MediaType.Book);

        Assert.Equal(3, results.Count);
        Assert.All(results, r =>
        {
            Assert.Equal("GoogleBooks", r.ExternalSource);
            Assert.Equal(MediaType.Book, r.MediaType);
        });
    }

    [Fact]
    public async Task SearchAsync_MapsCoreFields_ForFirstVolume()
    {
        var json = await LoadFixtureAsync("google-books-dune.json");
        var provider = ProviderReturning(json, out _);

        var dune = (await provider.SearchAsync("dune", MediaType.Book))[0];

        Assert.Equal("B1hSG45JCX4C", dune.ExternalId);
        Assert.Equal("Dune", dune.Title);
        Assert.Equal(1965, dune.ReleaseYear);
        Assert.Equal(["Frank Herbert"], dune.Creator);
    }

    [Fact]
    public async Task SearchAsync_TakesYearFromFullDate()
    {
        var json = await LoadFixtureAsync("google-books-dune.json");
        var provider = ProviderReturning(json, out _);

        // publishedDate "2019-08-01" -> 2019
        var messiah = (await provider.SearchAsync("dune", MediaType.Book))[1];

        Assert.Equal(2019, messiah.ReleaseYear);
        Assert.Equal(["Frank Herbert", "Brian Herbert"], messiah.Creator);
    }

    [Fact]
    public async Task SearchAsync_HandlesMissingAuthorsAndDate()
    {
        var json = await LoadFixtureAsync("google-books-dune.json");
        var provider = ProviderReturning(json, out _);

        // Third volume has no authors and no publishedDate.
        var encyclopedia = (await provider.SearchAsync("dune", MediaType.Book))[2];

        Assert.Null(encyclopedia.ReleaseYear);
        Assert.Empty(encyclopedia.Creator);   // never null — worst case empty list
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmpty_WhenNoItems()
    {
        var provider = ProviderReturning("""{ "kind": "books#volumes", "totalItems": 0 }""", out _);

        var results = await provider.SearchAsync("zzzznotarealbook", MediaType.Book);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_UrlEncodesTheQuery()
    {
        var provider = ProviderReturning("""{ "items": [] }""", out var handler);

        await provider.SearchAsync("ender's game", MediaType.Book);

        // The space and apostrophe must be percent-encoded in the outgoing URL.
        Assert.Contains("ender%27s%20game", handler.LastRequestUri!.Query);
    }

    [Fact]
    public async Task SearchAsync_AppendsApiKey_WhenConfigured()
    {
        var provider = ProviderReturning("""{ "items": [] }""", out var handler, apiKey: "secret123");

        await provider.SearchAsync("dune", MediaType.Book);

        Assert.Contains("key=secret123", handler.LastRequestUri!.Query);
    }

    [Fact]
    public async Task SearchAsync_OmitsApiKey_WhenNotConfigured()
    {
        var provider = ProviderReturning("""{ "items": [] }""", out var handler);

        await provider.SearchAsync("dune", MediaType.Book);

        Assert.DoesNotContain("key=", handler.LastRequestUri!.Query);
    }
}
