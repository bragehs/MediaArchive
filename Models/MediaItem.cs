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

    // Provider's own average, normalised to a 0-5 scale. Snapped to half-stars in the UI.
    public double? ExternalRating { get; set; }
    public int? ExternalRatingCount { get; set; }

    public int? UniverseId { get; set; }
    public Universe? Universe { get; set; }

    public int SeriesId { get; set; } = Series.StandaloneId;
    public Series? Series { get; set; }

    // Position within the series (book 3 of 4). Null when unknown or standalone.
    public int? SeriesPosition { get; set; }

    public List<MediaItemGenre> Genres { get; set; } = [];

    public List<MediaItemMood> Moods { get; set; } = [];

    public UserMediaItem? UserMediaItem { get; set; }

    [NotMapped] public abstract string? Creator { get; }

    // Total length, type-specific (pages / hours / runtime / episodes).
    [NotMapped] public abstract int? Length { get; }
}

public class Book : MediaItem
{
    public string? Author { get; set; }
    public int? PageCount { get; set; }

    [NotMapped] public override string? Creator => Author;
    [NotMapped] public override int? Length => PageCount;
}

public class Game : MediaItem
{
    public string? Developer { get; set; }
    public int? TimeToBeatHours { get; set; }

    [NotMapped] public override string? Creator => Developer;
    [NotMapped] public override int? Length => TimeToBeatHours;
}

public class Movie : MediaItem
{
    public string? Director { get; set; }
    public int? RuntimeMinutes { get; set; }

    [NotMapped] public override string? Creator => Director;
    [NotMapped] public override int? Length => RuntimeMinutes;
}

public class Show : MediaItem
{
    public string? Studio { get; set; }
    public int? EpisodeCount { get; set; }

    [NotMapped] public override string? Creator => Studio;
    [NotMapped] public override int? Length => EpisodeCount;
}
