namespace MediaArchive.Models;

// Join row only — Mood is a fixed enum, so there's no lookup table to point at.
public class MediaItemMood
{
    public int MediaItemId { get; set; }
    public MediaItem? MediaItem { get; set; }

    public Mood Mood { get; set; }
}
