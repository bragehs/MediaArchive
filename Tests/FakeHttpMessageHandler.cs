using System.Net;

namespace MediaArchive.Tests;

// A stand-in for the network. HttpClient delegates every request to its
// HttpMessageHandler; by injecting this one we make SearchAsync run its real
// parsing/mapping against a response WE control — no sockets, instant, deterministic.
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly IReadOnlyList<(string UrlFragment, string Json)> _routes;
    private readonly HttpStatusCode _status;

    public FakeHttpMessageHandler(string responseJson, HttpStatusCode status = HttpStatusCode.OK)
        : this([("", responseJson)], status)
    {
    }

    // Providers that need more than one call per operation (Open Library reads the
    // search index AND the work record) match on a fragment of the outgoing URL.
    public FakeHttpMessageHandler(
        IReadOnlyList<(string UrlFragment, string Json)> routes,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes = routes;
        _status = status;
    }

    // Lets a test assert what URL the provider actually built (encoding, params).
    public Uri? LastRequestUri { get; private set; }

    public List<Uri> RequestUris { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        RequestUris.Add(request.RequestUri!);

        var url = request.RequestUri!.ToString();
        var route = _routes.FirstOrDefault(r => url.Contains(r.UrlFragment, StringComparison.OrdinalIgnoreCase));

        if (route.Json is null)
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

        var response = new HttpResponseMessage(_status)
        {
            Content = new StringContent(route.Json, System.Text.Encoding.UTF8, "application/json")
        };

        return Task.FromResult(response);
    }
}
