using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

public class PopulationSummaryTests
{
    [Fact]
    public void AllThreeGroupsJoinWithAnAndBeforeTheLast()
    {
        var summary = PopulationSummary.Of(people: 12, men: 5, women: 5, children: 2);

        Assert.Equal("12 people: 5 men, 5 women and 2 children.", summary);
    }

    [Fact]
    public void AGroupNobodyIsInIsLeftOutRatherThanWrittenAsZero()
    {
        var summary = PopulationSummary.Of(people: 10, men: 5, women: 5, children: 0);

        Assert.Equal("10 people: 5 men and 5 women.", summary);
    }

    [Fact]
    public void OnlyOneGroupPresentNeedsNoJoiningWord()
    {
        var summary = PopulationSummary.Of(people: 5, men: 5, women: 0, children: 0);

        Assert.Equal("5 people: 5 men.", summary);
    }

    [Fact]
    public void SingularCountsUseSingularNouns()
    {
        var summary = PopulationSummary.Of(people: 3, men: 1, women: 1, children: 1);

        Assert.Equal("3 people: 1 man, 1 woman and 1 child.", summary);
    }

    [Fact]
    public void ASingleSurvivorReadsAsPersonNotPeople()
    {
        var summary = PopulationSummary.Of(people: 1, men: 1, women: 0, children: 0);

        Assert.Equal("1 person: 1 man.", summary);
    }
}
