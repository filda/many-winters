using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

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

    // Not MaxHunger 100 with variation 0.5: there halving, doubling and adding come out alike,
    // so mutants survive (docs/development.md, "Mutation testing").
    private static readonly SimulationRules HungerRules = new() { MaxHunger = 80f, MaxHungerVariation = 0.25f };

    [Fact]
    public void ADrawnMaxHungerStaysWithinTheVariationEitherWayOfTheAverage()
    {
        var drawn = Enumerable.Range(1, 1000).Select(seed => HungerRules.MaxHungerFor(TestIds.Person(seed)));

        Assert.All(drawn, maxHunger => Assert.InRange(maxHunger, 60f, 100f));
    }

    [Fact]
    public void TheSameIdAlwaysDrawsTheSameMaxHunger()
    {
        // Nothing saves this, so a person restored by id must draw the same value again.
        Assert.Equal(HungerRules.MaxHungerFor(TestIds.Person(7)), HungerRules.MaxHungerFor(TestIds.Person(7)));
    }

    // Why the draw runs through SeedHash: a starting band is a run of consecutive ids.
    [Fact]
    public void NeighbouringIdsDrawNoticeablyDifferentMaxHungers()
    {
        var drawn = Enumerable.Range(1, 12).Select(seed => HungerRules.MaxHungerFor(TestIds.Person(seed))).ToList();

        Assert.True(drawn.Max() - drawn.Min() > 20f, $"Twelve consecutive ids only spanned {drawn.Max() - drawn.Min():0.0}.");
    }

    [Fact]
    public void DrawsFallOnBothSidesOfTheAverageAboutEqually()
    {
        var below = Enumerable.Range(1, 1000).Count(seed => HungerRules.MaxHungerFor(TestIds.Person(seed)) < HungerRules.MaxHunger);

        Assert.InRange(below, 400, 600);
    }

    // The draw reads every bit of the spread except the one Person.SexOf takes, so lifespan
    // and sex stay independent.
    [Fact]
    public void HowLongSomebodyLastsDoesNotFollowFromTheirSex()
    {
        var longLastingWomen = Enumerable.Range(1, 1000)
            .Select(TestIds.Person)
            .Count(id => Person.SexOf(id) == Sex.Female && HungerRules.MaxHungerFor(id) > HungerRules.MaxHunger);

        // A quarter of 1000 if the two draws are independent, all or nothing if they are not.
        Assert.InRange(longLastingWomen, 200, 300);
    }

    [Fact]
    public void WithoutVariationEverybodyDrawsTheAverage()
    {
        var rules = new SimulationRules { MaxHunger = 80f, MaxHungerVariation = 0f };

        Assert.Equal(80f, rules.MaxHungerFor(TestIds.Person(7)));
        Assert.Equal(80f, rules.MaxHungerFor(TestIds.Person(8)));
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
