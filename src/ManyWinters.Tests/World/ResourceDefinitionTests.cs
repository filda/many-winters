using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

public class ResourceDefinitionTests
{
    [Fact]
    public void YieldMultiplierForReturnsOneWhenNoClimateYieldsAreSpecified()
    {
        var definition = new ResourceDefinition(TestCatalogs.Wood, "Wood", TestCatalogs.Woodcutting);

        Assert.Equal(1f, definition.YieldMultiplierFor(Climate.Cold));
        Assert.Equal(1f, definition.YieldMultiplierFor(Climate.Hot));
    }

    [Fact]
    public void YieldMultiplierForReturnsTheMatchingEntry()
    {
        var definition = new ResourceDefinition(
            TestCatalogs.Apple,
            "Apple",
            TestCatalogs.Foraging,
            ClimateYields: [new ClimateYield(Climate.Cold, 0.4f), new ClimateYield(Climate.Hot, 1.2f)]);

        Assert.Equal(0.4f, definition.YieldMultiplierFor(Climate.Cold));
        Assert.Equal(1.2f, definition.YieldMultiplierFor(Climate.Hot));
    }

    [Fact]
    public void YieldMultiplierForReturnsOneForAClimateWithNoEntry()
    {
        var definition = new ResourceDefinition(
            TestCatalogs.Apple,
            "Apple",
            TestCatalogs.Foraging,
            ClimateYields: [new ClimateYield(Climate.Cold, 0.4f)]);

        Assert.Equal(1f, definition.YieldMultiplierFor(Climate.Mild));
    }

    [Fact]
    public void IsInhospitableIsFalseWhenTheYieldMultiplierIsPositive()
    {
        var definition = new ResourceDefinition(
            TestCatalogs.Apple,
            "Apple",
            TestCatalogs.Foraging,
            ClimateYields: [new ClimateYield(Climate.Cold, 0.4f)]);

        Assert.False(definition.IsInhospitable(Climate.Cold));
    }

    [Fact]
    public void IsInhospitableIsTrueWhenTheYieldMultiplierIsZero()
    {
        var definition = new ResourceDefinition(
            TestCatalogs.Apple,
            "Apple",
            TestCatalogs.Foraging,
            ClimateYields: [new ClimateYield(Climate.Cold, 0f)]);

        Assert.True(definition.IsInhospitable(Climate.Cold));
    }

    [Fact]
    public void CanFellFellLeavesKindAndTicksToWitherDefaultToNotFellableAndNeverWithering()
    {
        var definition = new ResourceDefinition(TestCatalogs.Wood, "Wood", TestCatalogs.Woodcutting);

        Assert.False(definition.CanFell);
        Assert.Null(definition.FellLeavesKind);
        Assert.Equal(0f, definition.FellLeavesAmount);
        Assert.Equal(float.MaxValue, definition.TicksToWither);
    }
}
