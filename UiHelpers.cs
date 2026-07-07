using MediaArchive.Models;

namespace MediaArchive;

public static class UiHelpers
{
    public static string StatusGlyph(MediaStatus s) => s switch
    {
        MediaStatus.Completed => "✓",
        MediaStatus.InProgress => "▐▐",
        MediaStatus.Interested => "○",
        MediaStatus.Dropped => "✕",
        _ => "•"
    };

    public static string StatusLabel(MediaStatus s) => s switch
    {
        MediaStatus.Completed => "Completed",
        MediaStatus.InProgress => "In progress",
        MediaStatus.Interested => "Interested",
        MediaStatus.Dropped => "Dropped",
        _ => s.ToString()
    };

    public static string StatusClass(MediaStatus s) => s switch
    {
        MediaStatus.Completed => "st-completed",
        MediaStatus.InProgress => "st-inprogress",
        MediaStatus.Interested => "st-interested",
        MediaStatus.Dropped => "st-dropped",
        _ => ""
    };

    public static string TypeKey(MediaType t) => t.ToString().ToLowerInvariant();

    public static string TypeLabel(MediaType t) => t switch
    {
        MediaType.Book => "Book",
        MediaType.Game => "Game",
        MediaType.Movie => "Film",
        MediaType.Show => "Show",
        MediaType.Anime => "Anime",
        _ => t.ToString()
    };

    public static string TypeColorVar(MediaType t) => $"var(--{TypeKey(t)})";
}
