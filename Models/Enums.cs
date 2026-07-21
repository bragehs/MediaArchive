namespace MediaArchive.Models;

public enum MediaType
{
    Book,
    Game,
    Movie,
    Show
}

public enum MediaStatus
{
    Interested,
    InProgress,
    Completed,
    Dropped
}

public enum NoteKind
{
    Start,
    Progress,
    Finish
}

// How a work felt. Fixed, closed vocabulary — the point is that it's cheap enough
// to fill in every time, so the axis stays uniformly populated.
public enum Mood
{
    Dark,
    Adventurous,
    Tense,
    Funny,
    Emotional,
    Challenging,
    Mysterious,
    Sad,
    Reflective,
    Hopeful,
    Lighthearted
}

// How an item entered my life. Medium-agnostic by design — the axis only works
// for cross-media insight if it means the same thing for a book and a game.
public enum DiscoverySource
{
    Friend,
    Family,
    OnlineCommunity,
    SocialMedia,
    Algorithm,
    CriticReview,
    AwardOrList,
    Browsing,
    Franchise,
    Adaptation,
    Other
}

// Where/how a single pass was experienced. Type-specific values; the UI narrows
// the list by MediaType.
public enum ConsumptionContext
{
    Print,
    Ebook,
    Audiobook,
    Cinema,
    Streaming,
    PhysicalMedia,
    Broadcast,
    Pc,
    Console,
    Handheld,
    Mobile,
    Vr,
    Other
}
