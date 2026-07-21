namespace MediaArchive.Models;

// One row per time I wrote something during a pass — the diary WRITING.
public class EntryNote
{
    public int Id { get; set; }

    public int ConsumptionEntryId { get; set; }
    public ConsumptionEntry? ConsumptionEntry { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public NoteKind Kind { get; set; }

    // Snapshot of the entry's Effort when written — gives progress history.
    public int? EffortAtTime { get; set; }

    // Required for Start/Finish, optional for Progress — enforced in the log flow.
    public string? Text { get; set; }
}
