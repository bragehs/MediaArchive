namespace MediaArchive.Models;

public class Genre : INamed
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public int? ParentGenreId { get; set; }
    public Genre? ParentGenre { get; set; }
    public List<Genre> Subgenres { get; set; } = [];

    public List<MediaItemGenre> MediaItems { get; set; } = [];
}

public class MediaItemGenre
{
    public int MediaItemId { get; set; }
    public MediaItem? MediaItem { get; set; }

    public int GenreId { get; set; }
    public Genre? Genre { get; set; }
}
