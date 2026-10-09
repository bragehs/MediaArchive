using MediaArchive.Models;
using MediaArchive.Services.Providers;

namespace MediaArchive.Tests;

public class TmdbSearchTests
{
    private static (TmdbProvider Provider, FakeHttpMessageHandler Handler) ProviderReturning(string json)
    {
        var handler = new FakeHttpMessageHandler(json);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") };

        return (new TmdbProvider(client), handler);
    }

    [Fact]
    public async Task SearchAsync_MapsMovieTitleAndReleaseDate()
    {
        var (provider, handler) = ProviderReturning("""
            { "results": [ { "id": 603, "title": "The Matrix", "release_date": "1999-03-30", "poster_path": "/m.jpg" } ] }
            """);

        var result = Assert.Single(await provider.SearchAsync("matrix", MediaType.Movie));

        Assert.Contains("search/movie?query=matrix", handler.LastRequestUri!.ToString());
        Assert.Equal("603", result.ExternalId);
        Assert.Equal(MediaType.Movie, result.MediaType);
        Assert.Equal("The Matrix", result.Title);
        Assert.Equal(1999, result.ReleaseYear);
        Assert.Equal("https://image.tmdb.org/t/p/w500/m.jpg", result.ImageUrl);
    }

    [Fact]
    public async Task SearchAsync_MapsShowNameAndFirstAirDate()
    {
        var (provider, handler) = ProviderReturning("""
            { "results": [ { "id": 95396, "name": "Severance", "first_air_date": "2022-02-17", "poster_path": null } ] }
            """);

        var result = Assert.Single(await provider.SearchAsync("severance", MediaType.Show));

        Assert.Contains("search/tv?query=severance", handler.LastRequestUri!.ToString());
        Assert.Equal(MediaType.Show, result.MediaType);
        Assert.Equal("Severance", result.Title);
        Assert.Equal(2022, result.ReleaseYear);
        Assert.Null(result.ImageUrl);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenTmdbHasNoSuchMovie()
    {
        var client = new HttpClient(new FakeHttpMessageHandler([("tv/", "{}")]))
        {
            BaseAddress = new Uri("https://api.themoviedb.org/3/")
        };

        Assert.Null(await new TmdbProvider(client).GetByIdAsync("1", MediaType.Movie));
    }
}
