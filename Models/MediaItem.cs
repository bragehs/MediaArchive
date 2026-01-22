namespace MediaArchive.API.Models;

public abstract class MediaItem
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? AuthorOrStudio { get; set; }
    public string? Genre { get; set; }
}

public class Book : MediaItem 
{ 
    public int PageCount { get; set; } 
}

public class VideoGame : MediaItem 
{ 
    public string? Platform { get; set; } 
}