namespace MediaArchive.Models;

// An ordered, single-medium sequence (Mistborn Era 1, the MCU phase films).
// Distinct from Universe, which spans media types.
public class Series : INamed
{
    // Every item belongs to a series; anything that isn't part of one gets this row.
    public const int StandaloneId = 1;
    public const string StandaloneName = "Standalone";

    public int Id { get; set; }

    public required string Name { get; set; }

    public List<MediaItem> Items { get; set; } = [];
}
