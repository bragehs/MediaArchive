using MediaArchive.Models;
using MediaArchive.Services.Import;
using MediaArchive.Services.Logging;
using MediaArchive.Services.Providers;
using MediaArchive.Services.Queries;
using MediaArchive.Services.UserItems;
using Microsoft.Extensions.DependencyInjection;

namespace MediaArchive.Services.Native;

// One route per screen or action, named like a small HTTP API. SwiftName is
// the method tools/SwiftGen emits on the Swift Api; Args/Result are the
// records it generates structs for.
public sealed record Route(
    string Name,
    string SwiftName,
    Type? Args,
    Type? Result,
    Func<IServiceProvider, object?, Task<object?>> Handle);

public static class NativeRoutes
{
    public static readonly IReadOnlyList<Route> All =
    [
        Get("lexicon", "lexicon", _ => Task.FromResult(BuildLexicon())),

        Get("home", "home", async sp =>
        {
            var common = sp.GetRequiredService<CommonQueries>();
            var home = sp.GetRequiredService<HomeQueries>();
            return new HomePage(
                await home.GetWeeklyActivityAsync(),
                await home.GetOpenNowAsync(),
                await common.GetBacklogAsync(),
                await home.GetJustClosedAsync());
        }),

        Get("backlog", "backlog", sp => sp.GetRequiredService<CommonQueries>().GetBacklogAsync()),

        Get("library", "library", sp => sp.GetRequiredService<LibraryQueries>().GetLibraryAsync()),

        Get<QueryArgs, List<LibraryItem>>("library/search", "searchLibrary",
            (sp, a) => sp.GetRequiredService<LibraryQueries>().SearchArchiveAsync(a.Query)),

        Get("diary", "diary", async sp =>
        {
            var diary = sp.GetRequiredService<DiaryQueries>();
            var years = await diary.GetYearsAsync();
            return new DiaryIndex(years, years.Count > 0 ? await diary.GetYearAsync(years[0]) : null);
        }),

        Get<YearArgs, DiaryYear>("diary/year", "diaryYear",
            (sp, a) => sp.GetRequiredService<DiaryQueries>().GetYearAsync(a.Year)),

        Get<MonthArgs, DiaryMonthDetail>("diary/month", "diaryMonth",
            (sp, a) => sp.GetRequiredService<DiaryQueries>().GetMonthAsync(a.Year, a.Month)),

        Get("profile", "profile", sp => sp.GetRequiredService<ProfileQueries>().GetSnapshotAsync()),

        Get<ItemArgs, ItemPage?>("item", "item", async (sp, a) =>
        {
            var common = sp.GetRequiredService<CommonQueries>();
            var detail = await common.GetItemDetailAsync(a.UserMediaItemId);
            if (detail is null)
                return null;

            var history = detail.PassCount > 0
                ? await common.GetPassHistoryAsync(a.UserMediaItemId)
                : [];
            var vocabulary = await sp.GetRequiredService<MediaImportService>().GetVocabularyAsync();
            return new ItemPage(detail, history, vocabulary);
        }),

        Get<EntryArgs, EntryEffort?>("entry/effort", "entryEffort",
            (sp, a) => sp.GetRequiredService<CommonQueries>().GetEntryEffortAsync(a.EntryId)),

        Get("vocabulary", "vocabulary",
            sp => sp.GetRequiredService<MediaImportService>().GetVocabularyAsync()),

        Get<SearchArgs, IReadOnlyList<MediaSearchResultDto>>("search", "search",
            (sp, a) => sp.GetRequiredService<MediaSearchService>().SearchAsync(a.Query, a.MediaType)),

        Get<ExternalArgs, MediaItemDto?>("search/detail", "searchDetail",
            (sp, a) => sp.GetRequiredService<MediaSearchService>().GetByIdAsync(a.ExternalId, a.MediaType)),

        Get<QueryArgs, IReadOnlyList<SeasonDto>>("search/seasons", "seasons",
            (sp, a) => sp.GetRequiredService<MediaSearchService>().GetSeasonsAsync(a.Query)),

        Post<AddItemArgs, Created>("item/add", "addItem", async (sp, a) =>
            new Created(await sp.GetRequiredService<MediaImportService>().AddItemAsync(a.Item, a.Details))),

        Post<LogCompletedArgs, Created>("item/logCompleted", "logCompleted", async (sp, a) =>
            new Created(await sp.GetRequiredService<LoggingService>()
                .LogCompletedAsync(a.Item, a.Details, a.Start, a.Finish))),

        Post<UpdateDetailsArgs>("item/details", "updateDetails",
            (sp, a) => sp.GetRequiredService<UserItemService>().UpdateDetailsAsync(a.UserMediaItemId, a.Details)),

        Post<SetRuntimeArgs>("item/runtime", "setRuntime",
            (sp, a) => sp.GetRequiredService<UserItemService>().SetRuntimeAsync(a.UserMediaItemId, a.Value)),

        Post<SetRatingArgs>("item/rating", "setRating",
            (sp, a) => sp.GetRequiredService<UserItemService>().SetRatingAsync(a.UserMediaItemId, a.Rating)),

        Post<SetFavoriteArgs>("item/favorite", "setFavorite",
            (sp, a) => sp.GetRequiredService<UserItemService>().SetFavoriteAsync(a.UserMediaItemId, a.IsFavorite)),

        Post<StartPassArgs, Created>("pass/start", "startPass", async (sp, a) =>
            new Created(await sp.GetRequiredService<LoggingService>()
                .StartPassAsync(a.UserMediaItemId, a.Start, a.AllowConcurrent))),

        Post<ResumePassArgs, Created>("pass/resume", "resumePass", async (sp, a) =>
            new Created(await sp.GetRequiredService<LoggingService>().ResumePassAsync(a.EntryId, a.Start))),

        Post<AddNoteArgs>("pass/note", "addNote",
            (sp, a) => sp.GetRequiredService<LoggingService>().AddNoteAsync(a.EntryId, a.Note)),

        Post<FinishPassArgs>("pass/finish", "finishPass",
            (sp, a) => sp.GetRequiredService<LoggingService>().FinishPassAsync(a.EntryId, a.Finish)),
    ];

    public static Route? Find(string name) => All.FirstOrDefault(r => r.Name == name);

    private static Route Get<TResult>(string name, string swift, Func<IServiceProvider, Task<TResult>> handle) =>
        new(name, swift, null, typeof(TResult), async (sp, _) => await handle(sp));

    private static Route Get<TArgs, TResult>(string name, string swift,
        Func<IServiceProvider, TArgs, Task<TResult>> handle) =>
        new(name, swift, typeof(TArgs), typeof(TResult), async (sp, a) => await handle(sp, (TArgs)a!));

    private static Route Post<TArgs, TResult>(string name, string swift,
        Func<IServiceProvider, TArgs, Task<TResult>> handle) =>
        Get(name, swift, handle);

    private static Route Post<TArgs>(string name, string swift, Func<IServiceProvider, TArgs, Task> handle) =>
        new(name, swift, typeof(TArgs), null, async (sp, a) =>
        {
            await handle(sp, (TArgs)a!);
            return null;
        });

    private static Lexicon BuildLexicon() => new(
        [.. Enum.GetValues<MediaType>().Select(t => new TypeEntry(t, UiHelpers.TypeLabel(t),
            UiHelpers.LengthUnit(t), UiHelpers.RuntimeLabel(t), [.. UiHelpers.ContextsFor(t)]))],
        [.. Enum.GetValues<MediaStatus>().Select(s => new StatusEntry(s, UiHelpers.StatusLabel(s), UiHelpers.StatusGlyph(s)))],
        [.. Enum.GetValues<ConsumptionContext>().Select(c => new ContextEntry(c, UiHelpers.ContextLabel(c)))],
        [.. Enum.GetValues<DiscoverySource>().Select(d => new DiscoveryEntry(d, UiHelpers.DiscoveryLabel(d)))],
        [.. Enum.GetValues<DiaryEventKind>().Select(k => new KindEntry(k, UiHelpers.KindLabel(k)))],
        [.. Enum.GetValues<TagFacet>()]);
}
