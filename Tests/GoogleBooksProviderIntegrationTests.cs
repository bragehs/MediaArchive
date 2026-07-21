using System.Net;
using MediaArchive.Models;
using MediaArchive.Services.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace MediaArchive.Tests;

// Hits the REAL Google Books API. Excluded from the normal run:
//   dotnet test                                        -> runs everything
//   dotnet test --filter Category!=Integration         -> skips these
//   dotnet test --filter Category=Integration          -> only these
[Trait("Category", "Integration")]
public class GoogleBooksProviderIntegrationTests
{
    [Fact]
    public async Task SearchAsync_ReturnsMappedResults_ForKnownBook()
    {
        // A real provider over a real HttpClient — no fakes, no canned JSON.
        using var http = new HttpClient();
        var provider = new GoogleBooksProvider(http, Options.Create(LoadOptions()));

        var results = await WithRetry(() =>
            provider.SearchAsync("dune frank herbert"));

        // Google's ordering isn't guaranteed, so assert on the shape of the
        // data and that *some* result looks like the book we searched for,
        // rather than pinning an exact row.
        Assert.NotEmpty(results);

        Assert.All(results, r =>
        {
            Assert.Equal("GoogleBooks", r.ExternalSource);
            Assert.Equal(MediaType.Book, r.MediaType);
            Assert.False(string.IsNullOrWhiteSpace(r.ExternalId));
            Assert.False(string.IsNullOrWhiteSpace(r.Title));
            Assert.NotNull(r.Creator); // never null, worst case empty
        });

        // The parsing/mapping actually pulled real data through:
        Assert.Contains(results, r => r.Title.Contains("Dune", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(results, r => r.ReleaseYear is > 1900 and < 2100);
        Assert.Contains(results, r => r.Creator.Any(a => a.Contains("Herbert", StringComparison.OrdinalIgnoreCase)));
    }

    // Read the real key from the app's User Secrets (same store the app uses),
    // so this test gets its own quota. Falls back to keyless if none is set.
    private static GoogleBooksOptions LoadOptions()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(GoogleBooksProvider).Assembly)
            .AddEnvironmentVariables()
            .Build();

        var options = new GoogleBooksOptions();
        config.GetSection(GoogleBooksOptions.SectionName).Bind(options);
        return options;
    }

    // Google Books returns transient 503 (backendFailed) / 429 fairly often.
    // Retry a few times with backoff so the test measures OUR code, not
    // Google's momentary weather.
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