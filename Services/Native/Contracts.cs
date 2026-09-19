using MediaArchive.Models;
using MediaArchive.Services.Import;
using MediaArchive.Services.Providers;
using MediaArchive.Services.Queries;

namespace MediaArchive.Services.Native;

// Everything one screen needs, in one call. The property names are the
// contract with the generated Swift structs — tools/SwiftGen regenerates them.

public record HomePage(
    WeeklyActivity Weekly,
    List<OpenNowItem> OpenNow,
    List<CoverCard> OnDeck,
    JustClosedItem? JustClosed);

public record ItemPage(ItemDetail Detail, List<PassSummary> History, Vocabulary Vocabulary);

public record DiaryIndex(List<int> Years, DiaryYear? Current);

public record Created(int Id);

public record TypeEntry(MediaType Value, string Label, string Unit, string RuntimeLabel,
    List<ConsumptionContext> Contexts);

public record StatusEntry(MediaStatus Value, string Label, string Glyph);

public record ContextEntry(ConsumptionContext Value, string Label);

public record DiscoveryEntry(DiscoverySource Value, string Label);

public record KindEntry(DiaryEventKind Value, string Label);

// The UI vocabulary, served once at launch so the label tables and the
// which-context-fits-which-type rule live in UiHelpers only.
public record Lexicon(
    List<TypeEntry> Types,
    List<StatusEntry> Statuses,
    List<ContextEntry> Contexts,
    List<DiscoveryEntry> Discovery,
    List<KindEntry> Kinds,
    List<TagFacet> Facets);

public record ItemArgs(int UserMediaItemId);

public record YearArgs(int Year);

public record MonthArgs(int Year, int Month);

public record QueryArgs(string Query);

public record SearchArgs(string Query, MediaType MediaType);

public record ExternalArgs(string ExternalId, MediaType MediaType);

public record EntryArgs(int EntryId);

public record AddItemArgs(MediaItemDto Item, WorkDetails Details);

public record LogCompletedArgs(MediaItemDto Item, WorkDetails Details, PassStart Start,
    PassFinish Finish);

public record UpdateDetailsArgs(int UserMediaItemId, WorkDetails Details);

public record SetRuntimeArgs(int UserMediaItemId, int Value);

public record SetRatingArgs(int UserMediaItemId, int? Rating);

public record SetFavoriteArgs(int UserMediaItemId, bool IsFavorite);

public record StartPassArgs(int UserMediaItemId, PassStart Start, bool AllowConcurrent);

public record ResumePassArgs(int EntryId, PassStart Start);

public record AddNoteArgs(int EntryId, NoteInput Note);

public record FinishPassArgs(int EntryId, PassFinish Finish);
