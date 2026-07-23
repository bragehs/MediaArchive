namespace MediaArchive.Services.Providers;

// Providers publish averages on different scales; MediaItemDto.ExternalRating is
// always 0-10, matching the user rating's half-star basis so both share one widget.
public static class RatingScale
{
    public const double Max = 10.0;

    // TMDB: vote_average
    public static double? FromTen(double? value) => Normalise(value, 10.0);

    // IGDB: rating / total_rating
    public static double? FromHundred(double? value) => Normalise(value, 100.0);

    // Open Library: ratings_average is 0-5.
    public static double? FromFive(double? value) => Normalise(value, 5.0);

    private static double? Normalise(double? value, double sourceMax)
    {
        if (value is not > 0)
            return null;

        return Math.Clamp(value.Value / sourceMax * Max, 0, Max);
    }
}
