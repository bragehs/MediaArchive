using MediaArchive.Models;
using MediaArchive.Services.Import;
using MediaArchive.Services.Providers;
using MediaArchive.Services.Queries;

namespace MediaArchive.Services.Native;

public record HomePage(
    WeeklyActivity Weekly,
    List<OpenNowItem> OpenNow,
    List<CoverCard> OnDeck,
    JustClosedItem? JustClosed,
    LiveSession? Live);

public record ItemPage(ItemDetail Detail, List<PassSummary> History, Vocabulary Vocabulary,
    LiveSession? Live);

public record Created(int Id);

public record TypeEntry(MediaType Value, string Label, string Unit, string RuntimeLabel,
    List<ConsumptionContext> Contexts);

public record StatusEntry(MediaStatus Value, string Label, string Glyph);

public record ContextEntry(ConsumptionContext Value, string Label);

public record DiscoveryEntry(DiscoverySource Value, string Label);

public record KindEntry(ActivityKind Value, string Label);

public record Lexicon(
    List<TypeEntry> Types,
    List<StatusEntry> Statuses,
    List<ContextEntry> Contexts,
    List<DiscoveryEntry> Discovery,
    List<KindEntry> Kinds,
    List<TagFacet> Facets);

public record ItemArgs(int UserMediaItemId);

public record QueryArgs(string Query);

public record SearchArgs(string Query, MediaType MediaType);

public record ExternalArgs(string ExternalId, MediaType MediaType);

public record EntryArgs(int EntryId, int? ElapsedMinutes = null);

public record StartSessionArgs(int EntryId, DateTime? StartedAt);

public record AddItemArgs(MediaItemDto Item, WorkDetails Details);

public record LogCompletedArgs(MediaItemDto Item, WorkDetails Details, PassStart Start,
    PassFinish Finish);

public record UpdateDetailsArgs(int UserMediaItemId, WorkDetails Details);

public record SetRuntimeArgs(int UserMediaItemId, int Value);

public record SetRatingArgs(int UserMediaItemId, int? Rating);

public record SetFavoriteArgs(int UserMediaItemId, bool IsFavorite);

public record StartPassArgs(int UserMediaItemId, PassStart Start, bool AllowConcurrent);

public record ResumePassArgs(int EntryId, PassStart Start);

public record SetPassDatesArgs(int EntryId, PassDates Dates);

public record AddNoteArgs(int EntryId, NoteInput Note, SessionEnd? Session = null);

public record FinishPassArgs(int EntryId, PassFinish Finish, SessionEnd? Session = null);
