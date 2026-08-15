namespace MediaArchive.Models;

public enum TagFacet
{
    Mood,
    Theme,
    Style,
    Pacing
}

public class Tag : INamed
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public TagFacet? Facet { get; set; }

    public MediaType? AppliesTo { get; set; }

    public List<MediaItemTag> MediaItems { get; set; } = [];
}

public class MediaItemTag
{
    public int MediaItemId { get; set; }
    public MediaItem? MediaItem { get; set; }

    public int TagId { get; set; }
    public Tag? Tag { get; set; }
}
