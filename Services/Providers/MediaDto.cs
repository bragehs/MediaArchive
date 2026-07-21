using MediaArchive.Models;

namespace MediaArchive.Services.Providers;

public record MediaSearchResultDto(
    string ExternalSource,
    string ExternalId,
    MediaType MediaType,
    string Title,
    List<string> Creator,
    int? ReleaseYear);

public record MediaItemDto(
    string ExternalSource,
    string ExternalId,
    string Title,
    string? ImageUrl,
    int? ReleaseYear,
    MediaType MediaType,
    int? Length,
    string? Description,
    string? Creator,
    List<string> Genres,
    double? ExternalRating,
    int? ExternalRatingCount);