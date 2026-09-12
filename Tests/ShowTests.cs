using MediaArchive.Models;

namespace MediaArchive.Tests;

public class ShowTests
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

    [Fact]
    public void EstimatedMinutes_MultipliesEpisodesByRuntime()
    {
        var show = new Show { Title = "Severance", EpisodeCount = 9, EpisodeRuntime = 45 };

        Assert.Equal(405, show.EstimatedMinutes);
    }
}
