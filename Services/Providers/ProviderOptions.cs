namespace MediaArchive.Services.Providers;

public sealed class TmdbOptions
{
    public const string SectionName = "Tmdb";

    // The v4 "API Read Access Token" from the TMDB dashboard, sent as a bearer
    // token. Not the v3 API key, which goes in the query string instead.
    public string? ReadAccessToken { get; set; }
}

public sealed class IgdbOptions
{
    public const string SectionName = "Igdb";

    // IGDB authenticates through Twitch: exchange these for a bearer token.
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}