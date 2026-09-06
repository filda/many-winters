using ManyWinters.Core.World;

namespace ManyWinters.Tests.World;

public class SimulationRulesTests
{
    [Fact]
    public void DefaultIsTheShippedCalendar()
    {
        var rules = SimulationRules.Default;

        Assert.Equal(75, rules.TicksPerSeason);
        Assert.Equal(300, rules.TicksPerYear);
        Assert.Equal(10, rules.MaxLifespanYears);
        Assert.Equal(2f, rules.MaxInteractionDistance);
    }

    [Fact]
    public void AYearIsOneOfEverySeason()
    {
        var rules = new SimulationRules { TicksPerSeason = 7 };

        Assert.Equal(7 * 4, rules.TicksPerYear);
    }

    [Theory]
    [InlineData(0, Season.Spring)]
    [InlineData(2, Season.Spring)]
    [InlineData(3, Season.Summer)]
    [InlineData(6, Season.Autumn)]
    [InlineData(9, Season.Winter)]
    [InlineData(11, Season.Winter)]
    [InlineData(12, Season.Spring)]
    [InlineData(24, Season.Spring)]
    [InlineData(27, Season.Summer)]
    public void SeasonAtWalksTheCalendarInOrderAndWrapsAfterWinter(long tick, Season expected)
    {
        var rules = new SimulationRules { TicksPerSeason = 3 };

        Assert.Equal(expected, rules.SeasonAt(tick));
    }

    [Fact]
    public void OverridingOneRuleLeavesTheOthersAtTheirDefaults()
    {
        var rules = new SimulationRules { HungerPerTick = 5f };

        Assert.Equal(5f, rules.HungerPerTick);
        Assert.Equal(SimulationRules.Default.MaxHunger, rules.MaxHunger);
        Assert.Equal(SimulationRules.Default.IdleSearchRadius, rules.IdleSearchRadius);
    }
}
