namespace MediaArchive.Models;

public class Genre
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public List<MediaItemGenre> MediaItems { get; set; } = [];
}

public class MediaItemGenre
{
    public int MediaItemId { get; set; }
    public MediaItem? MediaItem { get; set; }

    public int GenreId { get; set; }
    public Genre? Genre { get; set; }
}
