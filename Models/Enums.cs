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

// PrimaryCreator is the uniform one (author / director / developer / lead studio)
// and is what Creator surfaces.
public enum CreditRole
{
    PrimaryCreator,
    Writer,
    Composer,
    Cast,
    Narrator,
    Studio
}

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