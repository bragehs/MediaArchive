using System.Net;
using MediaArchive.Models;
using MediaArchive.Services.Providers;

namespace MediaArchive.Tests;

// Hits the REAL Open Library API. Excluded from the normal run:
//   dotnet test                                        -> runs everything
//   dotnet test --filter Category!=Integration         -> skips these
//   dotnet test --filter Category=Integration          -> only these
[Trait("Category", "Integration")]
public class OpenLibraryProviderIntegrationTests
{
    [Fact]
    public async Task SearchAsync_ReturnsMappedResults_ForKnownBook()
    {
        // A real provider over a real HttpClient — no fakes, no canned JSON.
        using var http = CreateClient();
        var provider = new OpenLibraryProvider(http);

        var results = await WithRetry(() =>
            provider.SearchAsync("dune frank herbert", MediaType.Book));

        // Open Library's relevance ordering isn't guaranteed, so assert on the
        // shape of the data and that *some* result looks like the book we
        // searched for, rather than pinning an exact row.
        Assert.NotEmpty(results);

        Assert.All(results, r =>
        {
            Assert.Equal("OpenLibrary", r.ExternalSource);
            Assert.Equal(MediaType.Book, r.MediaType);
            Assert.False(string.IsNullOrWhiteSpace(r.ExternalId));
            Assert.False(string.IsNullOrWhiteSpace(r.Title));
            // Ids leave the provider bare, never as "/works/OL...".
            Assert.DoesNotContain('/', r.ExternalId);
        });

        Assert.Contains(results, r => r.Title.Contains("Dune", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(results, r => r.ReleaseYear is > 1900 and < 2100);
        Assert.Contains(results, r => r.ImageUrl is not null);
        Assert.All(results, r => Assert.True(r.ImageUrl is null || r.ImageUrl.StartsWith("https://")));
    }

    // The provider is the boundary where provider quirks die: Open Library
    // descriptions carry markdown links and editor footers, and the fields the
    // detail view needs are split across two endpoints.
    [Fact]
    public async Task GetByIdAsync_FillsTheDetailFields_FromBothEndpoints()
    {
        using var http = CreateClient();
        var provider = new OpenLibraryProvider(http);

        var results = await WithRetry(() => provider.SearchAsync("the way of kings sanderson", MediaType.Book));
        var first = results.First();

        var item = await WithRetry(() => provider.GetByIdAsync(first.ExternalId, MediaType.Book));

        Assert.NotNull(item);
        Assert.Equal(first.ExternalId, item.ExternalId);
        Assert.False(string.IsNullOrWhiteSpace(item.Title));

        // From the work record:
        Assert.False(string.IsNullOrWhiteSpace(item.Description));
        Assert.DoesNotContain("](", item.Description);
        Assert.DoesNotContain("<br", item.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<p>", item.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("&amp;", item.Description);
        Assert.DoesNotContain("&nbsp;", item.Description);
        Assert.DoesNotContain('\u00A0', item.Description);

        // From the search index:
        Assert.NotEmpty(item.Credits);
        Assert.All(item.Credits, c => Assert.Equal(CreditRole.Author, c.Role));
        Assert.True(item.Length is null or > 0);
        Assert.True(item.ExternalRating is null or (> 0 and <= RatingScale.Max));

        Assert.All(item.Genres, g =>
        {
            Assert.DoesNotContain(':', g!);
            Assert.DoesNotContain('=', g!);
        });

        if (item.ImageUrl is not null)
            Assert.StartsWith("https://covers.openlibrary.org/", item.ImageUrl);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_ForAnUnknownWork()
    {
        using var http = CreateClient();
        var provider = new OpenLibraryProvider(http);

        var item = await WithRetry(() => provider.GetByIdAsync("OL0000000W", MediaType.Book));

        Assert.Null(item);
    }

    // Open Library takes no key; it identifies clients by User-Agent instead,
    // and throttles requests that don't send a contactable one.
    private static HttpClient CreateClient()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd(OpenLibraryProvider.UserAgent);
        return http;
    }

    // Open Library returns transient 503 / 429 fairly often. Retry a few times
    // with backoff so the test measures OUR code, not Open Library's momentary
    // weather.
    private static async Task<T> WithRetry<T>(Func<Task<T>> action, int attempts = 4)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action();
            }
            catch (HttpRequestException e) when (
                attempt < attempts &&
                e.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.TooManyRequests)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt)); // 1s, 2s, 3s...
            }
        }
    }
}
