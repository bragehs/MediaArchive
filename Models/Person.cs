namespace MediaArchive.Models;

public class Person : INamed
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public List<MediaItemCredit> Credits { get; set; } = [];
}

public class MediaItemCredit
{
    public int MediaItemId { get; set; }
    public MediaItem? MediaItem { get; set; }

    public int PersonId { get; set; }
    public Person? Person { get; set; }

    public CreditRole Role { get; set; }
}
