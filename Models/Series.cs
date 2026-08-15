namespace MediaArchive.Models;

public class Series : INamed
{
    // Every item belongs to a series; anything that isn't part of one gets this row.
    public const int StandaloneId = 1;
    public const string StandaloneName = "Standalone";

    public int Id { get; set; }

    public required string Name { get; set; }

    public List<MediaItem> Items { get; set; } = [];
}
