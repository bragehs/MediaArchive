using MediaArchive.Models;

namespace MediaArchive.Services.Providers;

public record MediaSearchResultDto(
    string ExternalSource,
    string ExternalId,
    MediaType MediaType,
    string Title,
    string? ImageUrl,
    DateOnly? ReleaseDate)
{
    public int? ReleaseYear => ReleaseDate?.Year;
}

public record SeasonDto(
    int SeasonNumber,
    string Name,
    int? EpisodeCount,
    DateOnly? AirDate,
    string? ImageUrl);

public record CreditDto(string Name, CreditRole Role);

public record MediaItemDto(
    string ExternalSource,
    string ExternalId,
    string Title,
    string? ImageUrl,
    DateOnly? ReleaseDate,
    MediaType MediaType,
    int? Length,
    string? Description,
    List<CreditDto> Credits,
    List<string?> Genres,
    double? ExternalRating,
    int? ExternalRatingCount,
    List<string> Tags,
    int? EpisodeRuntime = null)
{
    public int? ReleaseYear => ReleaseDate?.Year;

    public string? Creator
    {
        get
        {
            var primaryRole = MediaType.PrimaryCreditRole();

            var joined = string.Join(", ",
                Credits.Where(c => c.Role == primaryRole).Select(c => c.Name));

            return joined.Length == 0 ? null : joined;
        }
    }
}