namespace MediaArchive.Models;

public class EntryNote
{
    public int Id { get; set; }

    public int ConsumptionEntryId { get; set; }
    public ConsumptionEntry? ConsumptionEntry { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public NoteKind Kind { get; set; }

    public int? EffortAtTime { get; set; }

    public string? Text { get; set; }
}
