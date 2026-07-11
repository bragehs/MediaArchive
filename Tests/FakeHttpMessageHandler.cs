using System.Net;

namespace MediaArchive.Tests;

// A stand-in for the network. HttpClient delegates every request to its
// HttpMessageHandler; by injecting this one we make SearchAsync run its real
// parsing/mapping against a response WE control — no sockets, instant, deterministic.
public sealed class FakeHttpMessageHandler(string responseJson, HttpStatusCode status = HttpStatusCode.OK)
    : HttpMessageHandler
{
    // Lets a test assert what URL the provider actually built (encoding, params).
    public Uri? LastRequestUri { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;

        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        };

        return Task.FromResult(response);
    }
}
