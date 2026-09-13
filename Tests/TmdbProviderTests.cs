using MediaArchive.Models;
using MediaArchive.Services.Providers;

namespace MediaArchive.Tests;

// Fast, offline, deterministic. Feeds a captured TMDb response through the
// provider's REAL parsing/mapping via a fake handler, in the same shape as
// OpenLibraryProviderTests.
public class TmdbProviderTests
{
    private static TmdbProvider ProviderReturning(string json)
    {
        var client = new HttpClient(new FakeHttpMessageHandler(json))
        {
            BaseAddress = new Uri("https://api.themoviedb.org/3/")
        };

        return new TmdbProvider(client);
    }

    private static async Task<string> LoadFixtureAsync(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        return await File.ReadAllTextAsync(path);
    }

    private static async Task<int?> EpisodeRuntimeFrom(string json)
    {
        var item = await ProviderReturning(json).GetByIdAsync("95396", MediaType.Show);

        Assert.NotNull(item);
        return item.EpisodeRuntime;
    }

    [Fact]
    public async Task GetByIdAsync_MapsCoreShowFields_FromCapturedResponse()
    {
        var json = await LoadFixtureAsync("tmdb-tv-severance.json");

        var item = await ProviderReturning(json).GetByIdAsync("95396", MediaType.Show);

        Assert.NotNull(item);
        Assert.Equal("Tmdb", item.ExternalSource);
        Assert.Equal("95396", item.ExternalId);
        Assert.Equal("Severance", item.Title);
        Assert.Equal(MediaType.Show, item.MediaType);
        Assert.Equal(2022, item.ReleaseYear);
        // Length is the episode count; the runtime rides along separately.
        Assert.Equal(19, item.Length);
        Assert.Contains(item.Credits, c => c is { Name: "Dan Erickson", Role: CreditRole.Director });
        Assert.Contains(item.Credits, c => c is { Name: "Apple TV+", Role: CreditRole.Studio });
    }

    [Fact]
    public async Task GetByIdAsync_AveragesTheEpisodeRunTimeArray()
    {
        var json = await LoadFixtureAsync("tmdb-tv-severance.json");

        // [90, 45, 45, 45] — the first entry alone would cost the whole show at pilot length.
        Assert.Equal(56, await EpisodeRuntimeFrom(json));
    }

    // One episode's length is not the series average, so it is never borrowed.
    [Fact]
    public async Task GetByIdAsync_IgnoresTheLastAiredEpisode_WhenEveryRunTimeIsNonPositive()
    {
        const string json = """
                            {
                              "id": 95396,
                              "name": "Severance",
                              "episode_run_time": [0],
                              "last_episode_to_air": { "runtime": 76 }
                            }
                            """;

        Assert.Null(await EpisodeRuntimeFrom(json));
    }

    [Fact]
    public async Task GetByIdAsync_IgnoresTheLastAiredEpisode_WhenEpisodeRunTimeIsAbsent()
    {
        const string json = """
                            {
                              "id": 95396,
                              "name": "Severance",
                              "last_episode_to_air": { "runtime": 76 }
                            }
                            """;

        Assert.Null(await EpisodeRuntimeFrom(json));
    }

    [Fact]
    public async Task GetByIdAsync_LeavesEpisodeRuntimeNull_WhenNeitherSourceHasOne()
    {
        const string json = """
                            {
                              "id": 95396,
                              "name": "Severance",
                              "episode_run_time": []
                            }
                            """;

        Assert.Null(await EpisodeRuntimeFrom(json));
    }

    [Fact]
    public async Task GetByIdAsync_NeverYieldsZero_WhenEveryRunTimeIsZero()
    {
        const string json = """
                            {
                              "id": 95396,
                              "name": "Severance",
                              "episode_run_time": [0, 0]
                            }
                            """;

        Assert.Null(await EpisodeRuntimeFrom(json));
    }

    [Fact]
    public void CanHandle_OnlyMoviesAndShows()
    {
        var provider = ProviderReturning("{}");

        Assert.True(provider.CanHandle(MediaType.Movie));
        Assert.True(provider.CanHandle(MediaType.Show));
        Assert.False(provider.CanHandle(MediaType.Book));
        Assert.False(provider.CanHandle(MediaType.Game));
    }
}
