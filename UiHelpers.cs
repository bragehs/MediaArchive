using MediaArchive.Models;
using MediaArchive.Services.Queries;

namespace MediaArchive;

public static class UiHelpers
{
    private static readonly Dictionary<MediaStatus, string> StatusGlyphs = new()
    {
        [MediaStatus.Completed] = "✓",
        [MediaStatus.InProgress] = "▐▐",
        [MediaStatus.Interested] = "○",
        [MediaStatus.Dropped] = "✕",
    };

    public static string StatusGlyph(MediaStatus s) => StatusGlyphs.GetValueOrDefault(s, "•");

    public static string StatusLabel(MediaStatus s) => s switch
    {
        MediaStatus.Completed => "Completed",
        MediaStatus.InProgress => "In progress",
        MediaStatus.Interested => "Interested",
        MediaStatus.Dropped => "Dropped",
        _ => s.ToString()
    };

    public static string TypeLabel(MediaType t) => t switch
    {
        MediaType.Book => "Book",
        MediaType.Game => "Game",
        MediaType.Movie => "Film",
        MediaType.Show => "Show",
        _ => t.ToString()
    };

    private static readonly Dictionary<DiscoverySource, string> DiscoveryLabels = new()
    {
        [DiscoverySource.Friend] = "Friend recommended",
        [DiscoverySource.Family] = "Family recommended",
        [DiscoverySource.OnlineCommunity] = "Online community",
        [DiscoverySource.SocialMedia] = "Social media",
        [DiscoverySource.Algorithm] = "Algorithm / store rec",
        [DiscoverySource.CriticReview] = "Critic or review",
        [DiscoverySource.AwardOrList] = "Award or list",
        [DiscoverySource.Browsing] = "Browsing",
        [DiscoverySource.Franchise] = "Followed the franchise",
        [DiscoverySource.Adaptation] = "Via an adaptation",
    };

    public static string DiscoveryLabel(DiscoverySource d) => DiscoveryLabels.GetValueOrDefault(d, "Other");

    private static readonly Dictionary<ConsumptionContext, string> ContextLabels = new()
    {
        [ConsumptionContext.Print] = "Print",
        [ConsumptionContext.Ebook] = "E-book",
        [ConsumptionContext.Audiobook] = "Audiobook",
        [ConsumptionContext.Cinema] = "Cinema",
        [ConsumptionContext.Streaming] = "Streaming",
        [ConsumptionContext.PhysicalMedia] = "Disc / physical",
        [ConsumptionContext.Broadcast] = "Broadcast TV",
        [ConsumptionContext.Pc] = "PC",
        [ConsumptionContext.Console] = "Console",
        [ConsumptionContext.Handheld] = "Handheld",
        [ConsumptionContext.Mobile] = "Mobile",
        [ConsumptionContext.Vr] = "VR",
    };

    public static string ContextLabel(ConsumptionContext c) => ContextLabels.GetValueOrDefault(c, "Other");

    public static ConsumptionContext[] ContextsFor(MediaType t) => t switch
    {
        MediaType.Book =>
        [
            ConsumptionContext.Print, ConsumptionContext.Ebook,
            ConsumptionContext.Audiobook, ConsumptionContext.Other
        ],
        MediaType.Game =>
        [
            ConsumptionContext.Pc, ConsumptionContext.Console, ConsumptionContext.Handheld,
            ConsumptionContext.Mobile, ConsumptionContext.Vr, ConsumptionContext.Other
        ],
        MediaType.Movie =>
        [
            ConsumptionContext.Cinema, ConsumptionContext.Streaming,
            ConsumptionContext.PhysicalMedia, ConsumptionContext.Broadcast, ConsumptionContext.Other
        ],
        _ =>
        [
            ConsumptionContext.Streaming, ConsumptionContext.PhysicalMedia,
            ConsumptionContext.Broadcast, ConsumptionContext.Other
        ]
    };

    public static string LengthUnit(MediaType t) => t switch
    {
        MediaType.Book => "pages",
        MediaType.Game => "hours",
        MediaType.Movie => "minutes",
        _ => "episodes"
    };

    public static string RuntimeLabel(MediaType t) => t switch
    {
        MediaType.Book => "Pages",
        MediaType.Game => "Hours to beat",
        MediaType.Movie => "Runtime (minutes)",
        _ => "Minutes per episode"
    };

    private static readonly Dictionary<ActivityKind, string> KindLabels = new()
    {
        [ActivityKind.Finished] = "Finished",
        [ActivityKind.Dropped] = "Dropped",
        [ActivityKind.Started] = "Started",
        [ActivityKind.Resumed] = "Resumed",
        [ActivityKind.Sat] = "Session",
    };

    public static string KindLabel(ActivityKind kind) => KindLabels.GetValueOrDefault(kind, "Logged");

    public static string Plural(int n, string one) => n == 1 ? one : one + "s";

    public static string Plural(int n, string one, string many) => n == 1 ? one : many;
}
