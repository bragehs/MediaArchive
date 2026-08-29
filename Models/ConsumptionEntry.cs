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

    // Set when this pass picks up an earlier dropped one.
    public int? ResumesEntryId { get; set; }
    public ConsumptionEntry? ResumesEntry { get; set; }

    // Null means zero; a resumed pass starts where its source left off.
    public int? StartingEffort { get; set; }

    public List<EntryNote> Notes { get; set; } = [];
}
