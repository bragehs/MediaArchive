using MediaArchive.Models;
using MediaArchive.Services.Queries;

namespace MediaArchive.Tests;

public class EffortMathTests
{
    [Fact]
    public void UnitsSpent_PrefersLoggedEffort_AboveTheResumedBaseline()
    {
        var book = new Book { Title = "Oathbringer", PageCount = 1243 };
        var entry = new ConsumptionEntry { Effort = 900, StartingEffort = 200 };

        Assert.Equal(700, EffortMath.UnitsSpent(book, entry));
    }

    [Fact]
    public void UnitsSpent_FallsBackToFullLength_WhenNoEffortWasLogged()
    {
        var game = new Game { Title = "The Witcher 3", TimeToBeatHours = 71 };

        Assert.Equal(71, EffortMath.UnitsSpent(game, new ConsumptionEntry()));
    }

    [Fact]
    public void UnitsSpent_IsNull_WhenNeitherEffortNorLengthIsKnown()
    {
        var game = new Game { Title = "Saros" };

        Assert.Null(EffortMath.UnitsSpent(game, new ConsumptionEntry()));
    }

    [Fact]
    public void ToMinutes_AppliesTheTypesConstant()
    {
        var book = new Book { Title = "The Final Empire", PageCount = 669 };

        Assert.Equal(836.25, EffortMath.ToMinutes(book, 669));
    }

    [Fact]
    public void ToMinutes_IsNull_WhenTheUnitCannotConvert()
    {
        var show = new Show { Title = "Detective Hole", EpisodeCount = 9, EpisodeRuntime = 0 };

        Assert.Null(EffortMath.ToMinutes(show, 9));
    }
}
