using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// How long anything has lasted, in the units the game counts in. A person's age and the time
// since a band arrived both read by this, so a wrong plural shows up twice.
public class DurationTextTests
{
    private const long TicksPerSeason = 100;
    private const long TicksPerYear = TicksPerSeason * 4;

    private static string For(long ticks) => DurationText.For(ticks, TicksPerYear, TicksPerSeason);

    [Fact]
    public void AnythingShortOfAWinterIsCountedInSeasons()
    {
        Assert.Equal("2 seasons", For(TicksPerSeason * 2));
    }

    [Fact]
    public void AWholeWinterSwitchesToCountingWinters()
    {
        Assert.Equal("1 winter", For(TicksPerYear));
    }

    // The first tick of the fifth season is a winter old, not four seasons: whole winters win as
    // soon as there is one.
    [Fact]
    public void TheWinterWinsTheMomentThereIsOne()
    {
        Assert.Equal("1 winter", For(TicksPerYear + 1));
    }

    [Fact]
    public void PartWintersAreNotRoundedUp()
    {
        Assert.Equal("1 winter", For((TicksPerYear * 2) - 1));
    }

    [Theory]
    [InlineData(0, "0 seasons")]
    [InlineData(TicksPerSeason, "1 season")]
    [InlineData(TicksPerSeason * 3, "3 seasons")]
    [InlineData(TicksPerYear * 2, "2 winters")]
    public void CountsOfOneAreSingular(long ticks, string expected)
    {
        Assert.Equal(expected, For(ticks));
    }
}
