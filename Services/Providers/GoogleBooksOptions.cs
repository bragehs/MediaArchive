namespace MediaArchive.Services.Providers;

// Bound from the "GoogleBooks" config section (User Secrets in dev).
// The API key is optional — without it requests still work but share a low
// public quota, so leaving it null is a valid configuration.
public sealed class GoogleBooksOptions
{
    public const string SectionName = "GoogleBooks";

    public string? ApiKey { get; set; }
}
