namespace MediaArchive.Models;

public class ConsumptionEntry
{
    public int Id { get; set; }

    public int UserMediaItemId { get; set; }
    public UserMediaItem? UserMediaItem { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    
    public PassOutcome? Outcome { get; set; }

    public int? RatingAtTime { get; set; }

    public int? Effort { get; set; }

    public ConsumptionContext? Context { get; set; }

    public List<EntryNote> Notes { get; set; } = [];
}
