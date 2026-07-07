namespace MediaArchive.Models;

public class Universe
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? LoreOfTheDayPrompt { get; set; }

    public List<MediaItem> Items { get; set; } = [];
}
