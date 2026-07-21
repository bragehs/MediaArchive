namespace MediaArchive.Models;

public class Universe : INamed
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? LoreOfTheDayPrompt { get; set; }

    public List<MediaItem> Items { get; set; } = [];
}
