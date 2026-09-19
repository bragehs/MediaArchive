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
    public void UnitsFor_FloorsSoAPrefillNeverClaimsMoreThanWasMeasured()
    {
        var game = new Game { Title = "Elden Ring", TimeToBeatHours = 121 };

        Assert.Equal(1, EffortMath.UnitsFor(game, 119));
        Assert.Equal(0, EffortMath.UnitsFor(game, 50));
    }

    [Fact]
    public void Suggest_PrefillsAFilmWithElapsedMinutes_AndOffersThemAsTheMissingRuntime()
    {
        var film = new Movie { Title = "Arrival" };

        var suggestion = EffortMath.Suggest(film, new ConsumptionEntry(), 116);

        Assert.Equal(116, suggestion.Effort);
        Assert.Equal(116, suggestion.Runtime);
    }

    [Fact]
    public void Suggest_AddsWholeEpisodesOntoTheLoggedEffort()
    {
        var show = new Show { Title = "Severance", EpisodeCount = 9, EpisodeRuntime = 45 };

        Assert.Equal(5, EffortMath.Suggest(show, new ConsumptionEntry { Effort = 3 }, 100).Effort);
    }

    [Fact]
    public void Suggest_LeavesAShowAlone_WhenItsEpisodeRuntimeIsUnknown()
    {
        var show = new Show { Title = "Detective Hole", EpisodeCount = 9 };

        var suggestion = EffortMath.Suggest(show, new ConsumptionEntry { Effort = 3 }, 100);

        Assert.Null(suggestion.Effort);
        Assert.Null(suggestion.Runtime);
    }

    [Fact]
    public void Suggest_NeverPrefillsAPrintBook()
    {
        var book = new Book { Title = "Oathbringer", PageCount = 1243 };

        Assert.Null(EffortMath.Suggest(book, new ConsumptionEntry { Context = ConsumptionContext.Print }, 60).Effort);
    }

    [Fact]
    public void Suggest_TakesElapsedHoursOffAnAudiobooksHoursLeft()
    {
        var book = new Book { Title = "The Hero of Ages", PageCount = 760, AudioHours = 22.5 };
        var entry = new ConsumptionEntry { Context = ConsumptionContext.Audiobook, Effort = 152 };

        // 152 of 760 pages is 4.5 h in, 18 h left; another 90 minutes leaves 16.5.
        Assert.Equal(16.5, EffortMath.Suggest(book, entry, 90).HoursLeft);
    }

    [Fact]
    public void SessionTarget_IsTheRuntimeForAFilm_OneEpisodeForAShow_AndNothingOpenEnded()
    {
        Assert.Equal(116, EffortMath.SessionTarget(new Movie { Title = "Arrival", RuntimeMinutes = 116 }));
        Assert.Equal(45, EffortMath.SessionTarget(new Show { Title = "Severance", EpisodeCount = 9, EpisodeRuntime = 45 }));
        Assert.Null(EffortMath.SessionTarget(new Game { Title = "Elden Ring", TimeToBeatHours = 121 }));
        Assert.Null(EffortMath.SessionTarget(new Movie { Title = "The Nation's Gambit", RuntimeMinutes = 0 }));
    }

    [Fact]
    public void ToMinutes_IsNull_WhenTheUnitCannotConvert()
    {
        var show = new Show { Title = "Detective Hole", EpisodeCount = 9, EpisodeRuntime = 0 };

        Assert.Null(EffortMath.ToMinutes(show, 9));
    }
}
