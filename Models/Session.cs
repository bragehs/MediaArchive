using System.ComponentModel.DataAnnotations.Schema;

namespace MediaArchive.Models;

// One sitting against a pass: how long I sat there, kept apart from how far I got.
public class Session
{
    public int Id { get; set; }

    public int ConsumptionEntryId { get; set; }
    public ConsumptionEntry? ConsumptionEntry { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }

    // Reported by the app when the sitting resolves; the activity only ever knows a total.
    public int PausedMinutes { get; set; }

    public int? EntryNoteId { get; set; }
    public EntryNote? EntryNote { get; set; }

    [NotMapped] public int? Minutes => EndedAt is { } ended
        ? Math.Max(0, (int)Math.Round((ended - StartedAt).TotalMinutes) - PausedMinutes)
        : null;
}
