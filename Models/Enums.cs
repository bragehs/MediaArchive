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

public enum PassOutcome
{
    Completed,
    Dropped
}

public enum NoteKind
{
    Start,
    Progress,
    Finish
}

public enum CreditRole
{
    Author,
    Director,
    Screenplay,
    Studio
}

public static class MediaTypeExtensions
{
    // The role Creator surfaces — every media type has exactly one headline credit.
    public static CreditRole PrimaryCreditRole(this MediaType mediaType)
    {
        return mediaType switch
        {
            MediaType.Book => CreditRole.Author,
            MediaType.Game => CreditRole.Studio,
            MediaType.Movie or MediaType.Show => CreditRole.Director,
            _ => throw new ArgumentOutOfRangeException(nameof(mediaType), mediaType, null)
        };
    }
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