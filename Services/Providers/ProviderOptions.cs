namespace MediaArchive.Services.Providers;

public sealed class TmdbOptions
{
    public const string SectionName = "Tmdb";

    // The v4 Read Access Token, sent as a bearer token — not the v3 API key.
    public string? ReadAccessToken { get; set; }
}

public sealed class IgdbOptions
{
    public const string SectionName = "Igdb";

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}