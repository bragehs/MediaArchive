using MediaArchive.Models;

namespace MediaArchive.Tests;

public class MediaItemTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(null)]
    public void MinutesPerUnit_IsNull_WhenEpisodeRuntimeIsNotPositive(int? episodeRuntime)
    {
        var show = new Show { Title = "Severance", EpisodeCount = 9, EpisodeRuntime = episodeRuntime };

        Assert.Null(show.MinutesPerUnit);
        Assert.Null(show.EstimatedMinutes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(null)]
    public void Length_IsNull_WhenTheStoredValueIsNotPositive(int? stored)
    {
        Assert.Null(new Book { Title = "b", PageCount = stored }.Length);
        Assert.Null(new Game { Title = "g", TimeToBeatHours = stored }.Length);
        Assert.Null(new Movie { Title = "m", RuntimeMinutes = stored }.Length);
        Assert.Null(new Show { Title = "s", EpisodeCount = stored }.Length);
    }

    // A TMDb runtime of 0 must not cost the film zero minutes in the totals.
    [Fact]
    public void EstimatedMinutes_IsNull_ForAZeroRuntimeFilm()
    {
        Assert.Null(new Movie { Title = "m", RuntimeMinutes = 0 }.EstimatedMinutes);
    }

    [Fact]
    public void EstimatedMinutes_MultipliesEpisodesByRuntime()
    {
        var show = new Show { Title = "Severance", EpisodeCount = 9, EpisodeRuntime = 45 };

        Assert.Equal(405, show.EstimatedMinutes);
    }
}
