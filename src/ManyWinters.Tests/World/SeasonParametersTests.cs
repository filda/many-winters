using ManyWinters.Core.World;

namespace ManyWinters.Tests.World;

public class SeasonParametersTests
{
    [Fact]
    public void ConstructorWiresTheGivenMappings()
    {
        var seasonParameters = new SeasonParameters(
            new Dictionary<Season, Climate> { [Season.Winter] = Climate.Hot },
            new Dictionary<Climate, float> { [Climate.Hot] = 3f },
            new Dictionary<Climate, float> { [Climate.Hot] = 0.5f });

        Assert.Equal(Climate.Hot, seasonParameters.ClimateFor(Season.Winter));
        Assert.Equal(3f, seasonParameters.HungerMultiplierFor(Climate.Hot));
        Assert.Equal(0.5f, seasonParameters.RegenMultiplierFor(Climate.Hot));
    }

    [Fact]
    public void ClimateForFallsBackToMildForAnUnmappedSeason()
    {
        var seasonParameters = new SeasonParameters(
            new Dictionary<Season, Climate>(),
            new Dictionary<Climate, float>(),
            new Dictionary<Climate, float>());

        Assert.Equal(Climate.Mild, seasonParameters.ClimateFor(Season.Winter));
    }

    [Fact]
    public void HungerMultiplierForFallsBackToOneForAnUnmappedClimate()
    {
        var seasonParameters = new SeasonParameters(
            new Dictionary<Season, Climate>(),
            new Dictionary<Climate, float>(),
            new Dictionary<Climate, float>());

        Assert.Equal(1f, seasonParameters.HungerMultiplierFor(Climate.Cold));
    }

    [Fact]
    public void RegenMultiplierForFallsBackToOneForAnUnmappedClimate()
    {
        var seasonParameters = new SeasonParameters(
            new Dictionary<Season, Climate>(),
            new Dictionary<Climate, float>(),
            new Dictionary<Climate, float>());

        Assert.Equal(1f, seasonParameters.RegenMultiplierFor(Climate.Cold));
    }

    [Theory]
    [InlineData(Season.Spring, Climate.Mild)]
    [InlineData(Season.Summer, Climate.Hot)]
    [InlineData(Season.Autumn, Climate.Mild)]
    [InlineData(Season.Winter, Climate.Cold)]
    public void DefaultMapsEachSeasonToTheExpectedClimate(Season season, Climate expected)
    {
        Assert.Equal(expected, SeasonParameters.Default.ClimateFor(season));
    }

    [Theory]
    [InlineData(Climate.Cold, 2f)]
    [InlineData(Climate.Mild, 1f)]
    [InlineData(Climate.Hot, 1f)]
    public void DefaultMapsEachClimateToTheExpectedHungerMultiplier(Climate climate, float expected)
    {
        Assert.Equal(expected, SeasonParameters.Default.HungerMultiplierFor(climate));
    }

    [Theory]
    [InlineData(Climate.Cold, 0f)]
    [InlineData(Climate.Mild, 1f)]
    [InlineData(Climate.Hot, 1f)]
    public void DefaultMapsEachClimateToTheExpectedRegenMultiplier(Climate climate, float expected)
    {
        Assert.Equal(expected, SeasonParameters.Default.RegenMultiplierFor(climate));
    }
}
