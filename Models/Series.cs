namespace MediaArchive.Models;

public class Series : INamed
{
    // A sentinel row rather than a nullable key: every item belongs to a series.
    public const int StandaloneId = 1;
    public const string StandaloneName = "Standalone";

    public int Id { get; set; }

    public required string Name { get; set; }

    public List<MediaItem> Items { get; set; } = [];
}
