using System.ComponentModel.DataAnnotations.Schema;

namespace MediaArchive.Models;

public class Session
{
    public int Id { get; set; }

    public int ConsumptionEntryId { get; set; }
    public ConsumptionEntry? ConsumptionEntry { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }

    // A total, not pause rows: the Live Activity only ever knows the sum.
    public int PausedMinutes { get; set; }

    public int? EntryNoteId { get; set; }
    public EntryNote? EntryNote { get; set; }

    [NotMapped] public int? Minutes => EndedAt is { } ended
        ? Math.Max(0, (int)Math.Round((ended - StartedAt).TotalMinutes) - PausedMinutes)
        : null;
}
