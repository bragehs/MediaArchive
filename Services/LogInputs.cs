using MediaArchive.Models;

namespace MediaArchive.Services;

// Facet and AppliesTo only apply when the tag doesn't exist yet — an existing row
// keeps the classification it was created with.
public record TagInput(string Name, TagFacet? Facet, MediaType? AppliesTo);

// Describes the WORK and my standing relationship to it — not any single pass.
// AudioHours is user-entered (no provider supplies it) and only meaningful for a
// Book consumed as an audiobook.
public record WorkDetails(
    List<string> Genres,
    List<TagInput> Tags,
    string? Universe,
    string? Series,
    int? SeriesPosition,
    DiscoverySource? Discovery,
    double? AudioHours = null);

// Opening a pass. No EndDate by design: you can't know it yet.
public record PassStart(
    DateOnly? StartDate,
    ConsumptionContext? Context,
    string? Note);

// Closing a pass. EndDate is required — that's the whole point of finishing.
public record PassFinish(
    DateOnly EndDate,
    int? Rating,
    int? Effort,
    string? Note,
    bool Dropped = false);

// Appended mid-pass, any number of times.
public record NoteInput(
    string Text,
    int? EffortAtTime);
