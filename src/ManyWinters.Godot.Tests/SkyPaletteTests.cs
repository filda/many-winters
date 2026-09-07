using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

public class SkyPaletteTests
{
    // Deliberately lopsided - a colour whose channels are far apart and whose average (0.4)
    // is nowhere near any of them, so nothing below passes by arithmetic coincidence.
    private static readonly Color Lopsided = new(0.1f, 0.3f, 0.8f);

    [Fact]
    public void DesaturatingByNothingLeavesTheColourExactlyAsItWas()
    {
        var result = SkyPalette.Desaturated(Lopsided, 0f);

        Assert.Equal(0.1f, result.R, 5);
        Assert.Equal(0.3f, result.G, 5);
        Assert.Equal(0.8f, result.B, 5);
    }

    [Fact]
    public void DesaturatingCompletelyLeavesTheGreyOfItsOwnAverage()
    {
        var result = SkyPalette.Desaturated(Lopsided, 1f);

        Assert.Equal(0.4f, result.R, 5);
        Assert.Equal(0.4f, result.G, 5);
        Assert.Equal(0.4f, result.B, 5);
    }

    [Fact]
    public void PartialDesaturationMovesEachChannelThatFractionOfTheWayToTheGrey()
    {
        // 0.1 -> 0.25, 0.3 -> 0.35, 0.8 -> 0.6: each channel half of its own distance to 0.4,
        // which is three different distances in two different directions.
        var result = SkyPalette.Desaturated(Lopsided, 0.5f);

        Assert.Equal(0.25f, result.R, 5);
        Assert.Equal(0.35f, result.G, 5);
        Assert.Equal(0.6f, result.B, 5);
    }

    [Fact]
    public void DesaturatingLeavesAlphaAlone()
    {
        // The fog sheet's own alpha comes from how unexplored the ground is, never from the
        // colour it fades toward.
        var result = SkyPalette.Desaturated(new Color(0.1f, 0.3f, 0.8f, 0.5f), 1f);

        Assert.Equal(0.5f, result.A, 5);
    }

    [Fact]
    public void TheFogFadesToSomethingAsBrightAsTheSkylineItMeets()
    {
        // The two are drawn side by side along the horizon, so a step in brightness there
        // would draw a line across the whole map.
        var fog = SkyPalette.FogFar;
        var horizon = SkyPalette.Horizon;

        var fogBrightness = (fog.R + fog.G + fog.B) / 3f;
        var horizonBrightness = (horizon.R + horizon.G + horizon.B) / 3f;

        Assert.Equal(horizonBrightness, fogBrightness, 3);
    }

    [Fact]
    public void TheFogKeepsMuchLessOfTheSkysColourThanTheSkyItself()
    {
        // The point of desaturating it at all: unexplored ground that is as blue as the air
        // above it stops reading as ground.
        var fogSpread = SkyPalette.FogFar.B - SkyPalette.FogFar.R;
        var skySpread = SkyPalette.Horizon.B - SkyPalette.Horizon.R;

        Assert.True(fogSpread > 0f, "a trace of the sky's blue should survive");
        Assert.True(fogSpread < skySpread / 2f, "but well under half of it");
    }

    [Fact]
    public void TheSkyGetsDarkerAndBluerTheHigherItGoes()
    {
        // The whole point of having a gradient rather than one flat colour.
        Assert.True(SkyPalette.Zenith.R < SkyPalette.Horizon.R);
        Assert.True(SkyPalette.Zenith.B - SkyPalette.Zenith.R > SkyPalette.Horizon.B - SkyPalette.Horizon.R);
    }

    [Fact]
    public void TheStreaksAreLighterThanTheSkyTheyLieOn()
    {
        // They are wisps catching the light, not shadows - see the shader's own doc comment.
        Assert.True(SkyPalette.Streak.R > SkyPalette.Horizon.R);
        Assert.True(SkyPalette.Streak.G > SkyPalette.Horizon.G);
        Assert.True(SkyPalette.Streak.B > SkyPalette.Horizon.B);
    }
}
