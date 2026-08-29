using MediaArchive.Models;

namespace MediaArchive;

public static class UiHelpers
{
    public static string StatusGlyph(MediaStatus s) => s switch
    {
        MediaStatus.Completed => "✓",
        MediaStatus.InProgress => "▐▐",
        MediaStatus.Interested => "○",
        MediaStatus.Dropped => "✕",
        _ => "•"
    };

    public static string StatusLabel(MediaStatus s) => s switch
    {
        MediaStatus.Completed => "Completed",
        MediaStatus.InProgress => "In progress",
        MediaStatus.Interested => "Interested",
        MediaStatus.Dropped => "Dropped",
        _ => s.ToString()
    };

    public static string TypeKey(MediaType t) => t.ToString().ToLowerInvariant();

    public static string TypeLabel(MediaType t) => t switch
    {
        MediaType.Book => "Book",
        MediaType.Game => "Game",
        MediaType.Movie => "Film",
        MediaType.Show => "Show",
        _ => t.ToString()
    };

    public static string TypeColorVar(MediaType t) => $"var(--{TypeKey(t)})";

    public static string DiscoveryLabel(DiscoverySource d) => d switch
    {
        DiscoverySource.Friend => "Friend recommended",
        DiscoverySource.Family => "Family recommended",
        DiscoverySource.OnlineCommunity => "Online community",
        DiscoverySource.SocialMedia => "Social media",
        DiscoverySource.Algorithm => "Algorithm / store rec",
        DiscoverySource.CriticReview => "Critic or review",
        DiscoverySource.AwardOrList => "Award or list",
        DiscoverySource.Browsing => "Browsing",
        DiscoverySource.Franchise => "Followed the franchise",
        DiscoverySource.Adaptation => "Via an adaptation",
        _ => "Other"
    };

    public static string ContextLabel(ConsumptionContext c) => c switch
    {
        ConsumptionContext.Print => "Print",
        ConsumptionContext.Ebook => "E-book",
        ConsumptionContext.Audiobook => "Audiobook",
        ConsumptionContext.Cinema => "Cinema",
        ConsumptionContext.Streaming => "Streaming",
        ConsumptionContext.PhysicalMedia => "Disc / physical",
        ConsumptionContext.Broadcast => "Broadcast TV",
        ConsumptionContext.Pc => "PC",
        ConsumptionContext.Console => "Console",
        ConsumptionContext.Handheld => "Handheld",
        ConsumptionContext.Mobile => "Mobile",
        ConsumptionContext.Vr => "VR",
        _ => "Other"
    };

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
}
