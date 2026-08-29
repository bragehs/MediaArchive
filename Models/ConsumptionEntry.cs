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

    // A pass picked up after an earlier one was dropped. The chain keeps the
    // dormant gap visible — two intervals with a hole between them, rather than
    // one interval pretending the quiet months were active.
    public int? ResumesEntryId { get; set; }
    public ConsumptionEntry? ResumesEntry { get; set; }

    // Where this pass begins on the item's effort scale. Null means zero; a
    // resumed pass starts where the pass it continues left off.
    public int? StartingEffort { get; set; }

    public List<EntryNote> Notes { get; set; } = [];
}
