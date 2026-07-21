using MediaArchive.Models;
using MediaArchive.Services.Providers;

namespace MediaArchive.Services;

public static class MediaItemMapper
{
    public static MediaItem ToEntity(MediaItemDto dto)
    {
        MediaItem item = dto.MediaType switch
        {
            MediaType.Book => new Book { Title = dto.Title, Author = dto.Creator, PageCount = dto.Length },
            MediaType.Game => new Game { Title = dto.Title, Developer = dto.Creator, TimeToBeatHours = dto.Length },
            MediaType.Movie => new Movie { Title = dto.Title, Director = dto.Creator, RuntimeMinutes = dto.Length },
            MediaType.Show => new Show { Title = dto.Title, Studio = dto.Creator, EpisodeCount = dto.Length },
            _ => throw new ArgumentOutOfRangeException(nameof(dto), dto.MediaType, "Unsupported media type.")
        };

        item.MediaType = dto.MediaType;
        item.ImageUrl = dto.ImageUrl;
        item.ReleaseYear = dto.ReleaseYear;
        item.Description = dto.Description;
        item.ExternalId = dto.ExternalId;
        item.ExternalSource = dto.ExternalSource;
        item.ExternalRating = dto.ExternalRating;
        item.ExternalRatingCount = dto.ExternalRatingCount;

        return item;
    }
}
