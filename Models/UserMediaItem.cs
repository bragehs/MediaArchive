namespace MediaArchive.Models;

public class UserMediaItem
{
    public int Id { get; set; }

    public int MediaItemId { get; set; }
    public MediaItem? MediaItem { get; set; }

    public MediaStatus Status { get; set; } = MediaStatus.Interested;

    public int? Rating { get; set; }

    public bool IsFavorite { get; set; }

    public DiscoverySource? Discovery { get; set; }

    public DateOnly AddedDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public List<ConsumptionEntry> Entries { get; set; } = [];
}