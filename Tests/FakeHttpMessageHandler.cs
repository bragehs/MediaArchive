using System.Net;

namespace MediaArchive.Tests;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly IReadOnlyList<(string UrlFragment, string Json)> _routes;
    private readonly HttpStatusCode _status;

    public FakeHttpMessageHandler(string responseJson, HttpStatusCode status = HttpStatusCode.OK)
        : this([("", responseJson)], status)
    {
    }

    public FakeHttpMessageHandler(
        IReadOnlyList<(string UrlFragment, string Json)> routes,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes = routes;
        _status = status;
    }

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
