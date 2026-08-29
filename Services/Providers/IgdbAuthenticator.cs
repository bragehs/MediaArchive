using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace MediaArchive.Services.Providers;

public sealed class IgdbAuthenticator(IHttpClientFactory httpClientFactory, IOptions<IgdbOptions> options)
{
    private const string TokenUrl = "https://id.twitch.tv/oauth2/token";

    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _token;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (IsValid())
            return _token!;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (IsValid())
                return _token!;

            var url = $"{TokenUrl}?client_id={options.Value.ClientId}"
                    + $"&client_secret={options.Value.ClientSecret}"
                    + "&grant_type=client_credentials";

            using var client = httpClientFactory.CreateClient();
            var response = await client.PostAsync(url, null, cancellationToken);
            response.EnsureSuccessStatusCode();

            var token = await response.Content
                .ReadFromJsonAsync<TwitchToken>(cancellationToken)
                ?? throw new InvalidOperationException("Twitch returned an empty token response.");

            _token = token.AccessToken;
            // Refresh a minute early to dodge clock skew and in-flight expiry.
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn - 60);

            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool IsValid()
    {
        return _token is not null && DateTimeOffset.UtcNow < _expiresAt;
    }

    private sealed record TwitchToken(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
