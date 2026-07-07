using System.ComponentModel.DataAnnotations.Schema;

namespace MediaArchive.Models;

public abstract class MediaItem
{
    public int Id { get; set; }

    public required string Title { get; set; }

    public string? ImageUrl { get; set; }

    public int? ReleaseYear { get; set; }

    public MediaType MediaType { get; set; }

    public string? Description { get; set; }

    public string? ExternalId { get; set; }
    public string? ExternalSource { get; set; }

    public int? UniverseId { get; set; }
    public Universe? Universe { get; set; }

    public List<MediaItemGenre> Genres { get; set; } = [];

    public UserMediaItem? UserMediaItem { get; set; }

    [NotMapped]
    public abstract string? Creator { get; }
}

public class Book : MediaItem
{
    public string? Author { get; set; }

    [NotMapped]
    public override string? Creator => Author;
}

public class Game : MediaItem
{
    public string? Developer { get; set; }

    [NotMapped]
    public override string? Creator => Developer;
}

public class Movie : MediaItem
{
    public string? Director { get; set; }

    [NotMapped]
    public override string? Creator => Director;
}

public class Show : MediaItem
{
    public string? Studio { get; set; }

    [NotMapped]
    public override string? Creator => Studio;
}

public class Anime : MediaItem
{
    public string? Studio { get; set; }

    [NotMapped]
    public override string? Creator => Studio;
}
