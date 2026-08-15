namespace MediaArchive.Services.Providers;

public static class RatingScale
{
    public const double Max = 10.0;

    public static double? FromTen(double? value) => Normalise(value, 10.0);

    public static double? FromHundred(double? value) => Normalise(value, 100.0);

    public static double? FromFive(double? value) => Normalise(value, 5.0);

    private static double? Normalise(double? value, double sourceMax)
    {
        if (value is not > 0)
            return null;

        return Math.Clamp(value.Value / sourceMax * Max, 0, Max);
    }
}
