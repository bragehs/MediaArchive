using MediaArchive.Models;

namespace MediaArchive.Services;

// Describes the WORK and my standing relationship to it — not any single pass.
public record WorkDetails(
    List<string> Genres,
    List<Mood> Moods,
    string? Universe,
    string? Series,
    int? SeriesPosition,
    DiscoverySource? Discovery);

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
