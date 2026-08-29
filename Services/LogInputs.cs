using MediaArchive.Models;

namespace MediaArchive.Services;

// Facet and AppliesTo only apply when the tag doesn't exist yet.
public record TagInput(string Name, TagFacet? Facet, MediaType? AppliesTo);

public record WorkDetails(
    List<string> Genres,
    List<TagInput> Tags,
    string? Universe,
    string? Series,
    int? SeriesPosition,
    DiscoverySource? Discovery,
    double? AudioHours = null);

public record PassStart(
    DateOnly? StartDate,
    ConsumptionContext? Context,
    string? Note);

public record PassFinish(
    DateOnly EndDate,
    int? Rating,
    int? Effort,
    string? Note,
    bool Dropped = false);

public record NoteInput(
    string Text,
    int? EffortAtTime);
