namespace MediaArchive.Services.Providers;

public sealed class GoogleBooksOptions
{
    public const string SectionName = "GoogleBooks";

    public string? ApiKey { get; set; }
}

public sealed class TmdbOptions
{
    public const string SectionName = "Tmdb";

    public string? ApiKey { get; set; }
}

public sealed class IgdbOptions
{
    public const string SectionName = "Igdb";

    // IGDB authenticates through Twitch: exchange these for a bearer token.
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}