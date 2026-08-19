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
}
