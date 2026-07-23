using System.ComponentModel.DataAnnotations.Schema;

namespace MediaArchive.Models;

public abstract class MediaItem
{
    public int Id { get; set; }

    public required string Title { get; set; }

    public string? ImageUrl { get; set; }

    // Partial provider dates are stored as Jan 1.
    public DateOnly? ReleaseDate { get; set; }

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

    public List<MediaItemTag> Tags { get; set; } = [];

    public List<MediaItemCredit> Credits { get; set; } = [];

    public UserMediaItem? UserMediaItem { get; set; }

    // Needs Credits and their People Included — this reads loaded rows, not the DB.
    [NotMapped]
    public string? Creator
    {
        get
        {
            var primaryRole = MediaType.PrimaryCreditRole();

            var names = Credits
                .Where(c => c.Role == primaryRole && c.Person is not null)
                .Select(c => c.Person!.Name);

            var joined = string.Join(", ", names);
            return joined.Length == 0 ? null : joined;
        }
    }

    // Total length, type-specific (pages / hours / runtime / episodes).
    [NotMapped] public abstract int? Length { get; }

    // Null = can't be converted, so the item is excluded from time totals
    // rather than counted as zero.
    [NotMapped] public abstract double? MinutesPerUnit { get; }

    [NotMapped]
    public int? EstimatedMinutes =>
        Length is { } length && MinutesPerUnit is { } perUnit
            ? (int)Math.Round(length * perUnit)
            : null;
}

public class Book : MediaItem
{
    public const double MinutesPerPage = 1.25;

    public int? PageCount { get; set; }

    [NotMapped] public override int? Length => PageCount;
    [NotMapped] public override double? MinutesPerUnit => MinutesPerPage;
}

public class Game : MediaItem
{
    public int? TimeToBeatHours { get; set; }

    [NotMapped] public override int? Length => TimeToBeatHours;
    [NotMapped] public override double? MinutesPerUnit => 60;
}

public class Movie : MediaItem
{
    public int? RuntimeMinutes { get; set; }

    [NotMapped] public override int? Length => RuntimeMinutes;
    [NotMapped] public override double? MinutesPerUnit => 1;
}

public class Show : MediaItem
{
    public int? EpisodeCount { get; set; }

    // Episodes have no usable average — a sitcom and a drama differ by 3x.
    public int? EpisodeRuntime { get; set; }

    [NotMapped] public override int? Length => EpisodeCount;
    [NotMapped] public override double? MinutesPerUnit => EpisodeRuntime;
}