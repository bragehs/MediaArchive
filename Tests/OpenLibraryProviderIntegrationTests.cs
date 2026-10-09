using System.Net;
using MediaArchive.Models;
using MediaArchive.Services.Providers;

namespace MediaArchive.Tests;

[Trait("Category", "Integration")]
public class OpenLibraryProviderIntegrationTests
{
    [Fact]
    public async Task SearchAsync_ReturnsMappedResults_ForKnownBook()
    {
        using var http = CreateClient();
        var provider = new OpenLibraryProvider(http);

        var results = await WithRetry(() =>
            provider.SearchAsync("dune frank herbert", MediaType.Book));

        // Relevance ordering isn't guaranteed, so this asserts on shape rather than an exact row.
        Assert.NotEmpty(results);

        Assert.All(results, r =>
        {
            Assert.Equal("OpenLibrary", r.ExternalSource);
            Assert.Equal(MediaType.Book, r.MediaType);
            Assert.False(string.IsNullOrWhiteSpace(r.ExternalId));
            Assert.False(string.IsNullOrWhiteSpace(r.Title));
            Assert.DoesNotContain('/', r.ExternalId);
        });

        Assert.Contains(results, r => r.Title.Contains("Dune", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(results, r => r.ReleaseYear is > 1900 and < 2100);
        Assert.Contains(results, r => r.ImageUrl is not null);
        Assert.All(results, r => Assert.True(r.ImageUrl is null || r.ImageUrl.StartsWith("https://")));
    }

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

        Assert.False(string.IsNullOrWhiteSpace(item.Description));
        Assert.DoesNotContain("](", item.Description);
        Assert.DoesNotContain("<br", item.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<p>", item.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("&amp;", item.Description);
        Assert.DoesNotContain("&nbsp;", item.Description);
        Assert.DoesNotContain('\u00A0', item.Description);

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

    // Open Library throttles clients that don't send a contactable User-Agent.
    private static HttpClient CreateClient()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd(OpenLibraryProvider.UserAgent);
        return http;
    }

    // Retried because Open Library returns transient 503/429s; the test measures our code, not their uptime.
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
                await Task.Delay(TimeSpan.FromSeconds(attempt));
            }
        }
    }
}
