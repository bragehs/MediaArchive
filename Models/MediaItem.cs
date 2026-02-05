namespace MediaArchive.API.Models;

public abstract class MediaItem<TId>
{
    public required TId Id { get; set;  }
    public required string Title { get; set; }
    public required string ImageUrl { get; set; }
    public string? ReleaseYear { get; set; }
}

public class Book : MediaItem<string> 
{ 
    public required string Author { get; set; } 
}

public class VideoGame : MediaItem<int>
{ 
    public required string Developer { get; set; } 
}