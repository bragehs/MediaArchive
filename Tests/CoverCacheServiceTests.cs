using System.Net;
using System.Net.Http.Headers;
using MediaArchive.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace MediaArchive.Tests;

public class CoverCacheServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cover-cache-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class StubHandler(
        HttpStatusCode status,
        byte[]? body = null,
        string mediaType = "image/jpeg",
        Exception? throws = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (throws is not null)
                return Task.FromException<HttpResponseMessage>(throws);

            var response = new HttpResponseMessage(status);

            if (body is not null)
            {
                response.Content = new ByteArrayContent(body);
                response.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            }

            return Task.FromResult(response);
        }
    }

    private CoverCacheService ServiceWith(HttpMessageHandler handler) =>
        new(new HttpClient(handler), Path.Combine(_root, "covers"), NullLogger<CoverCacheService>.Instance);

    [Fact]
    public async Task TryCacheAsync_WritesFile_AndReturnsServablePath()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var service = ServiceWith(new StubHandler(HttpStatusCode.OK, bytes));

        var path = await service.TryCacheAsync("https://covers/x.jpg", "OpenLibrary", "OL893415W");

        Assert.Equal("/covers/OpenLibrary-OL893415W.jpg", path);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(
            Path.Combine(_root, "covers", "OpenLibrary-OL893415W.jpg")));
    }

    [Fact]
    public async Task TryCacheAsync_UsesExtension_FromContentType()
    {
        var service = ServiceWith(new StubHandler(HttpStatusCode.OK, [1], "image/png"));

        var path = await service.TryCacheAsync("https://images.igdb.com/x.png", "IGDB", "co1r7f");

        Assert.Equal("/covers/IGDB-co1r7f.png", path);
    }

    [Fact]
    public async Task TryCacheAsync_ReturnsNull_WhenProviderFails()
    {
        var service = ServiceWith(new StubHandler(HttpStatusCode.NotFound));

        Assert.Null(await service.TryCacheAsync("https://covers/x.jpg", "OpenLibrary", "OL1W"));
    }

    [Fact]
    public async Task TryCacheAsync_ReturnsNull_WhenRequestTimesOut()
    {
        var service = ServiceWith(new StubHandler(HttpStatusCode.OK,
            throws: new TaskCanceledException("timeout")));

        Assert.Null(await service.TryCacheAsync("https://covers/x.jpg", "OpenLibrary", "OL1W"));
    }

    [Fact]
    public async Task TryCacheAsync_LeavesNoTempFile_WhenDownloadFails()
    {
        var service = ServiceWith(new StubHandler(HttpStatusCode.OK,
            throws: new HttpRequestException("boom")));

        await service.TryCacheAsync("https://covers/x.jpg", "OpenLibrary", "OL1W");

        var stale = Directory.Exists(Path.Combine(_root, "covers"))
            ? Directory.GetFiles(Path.Combine(_root, "covers"))
            : [];
        Assert.Empty(stale);
    }

    [Fact]
    public async Task TryCacheAsync_ReturnsNull_WhenItemHasNoProviderIdentity()
    {
        var service = ServiceWith(new StubHandler(HttpStatusCode.OK, [1]));

        Assert.Null(await service.TryCacheAsync("https://covers/x.jpg", null, null));
        Assert.Null(await service.TryCacheAsync(null, "OpenLibrary", "OL1W"));
    }

    [Fact]
    public async Task TryCacheAsync_PropagatesCancellation_FromCaller()
    {
        var service = ServiceWith(new StubHandler(HttpStatusCode.OK,
            throws: new TaskCanceledException("cancelled")));
        var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.TryCacheAsync("https://covers/x.jpg", "OpenLibrary", "OL1W", cancelled.Token));
    }
}
