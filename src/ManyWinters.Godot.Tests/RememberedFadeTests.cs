using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

public class RememberedFadeTests
{
    // Base modulate is never plain white in practice, and each channel here is a different
    // distance from 1, so a tint on the wrong channel or dropped entirely cannot pass.
    private static readonly Color Base = new(0.9f, 0.6f, 0.4f);

    [Fact]
    public void AFreshFadeIsInSightAndNotMoving()
    {
        var fade = new RememberedFade();

        Assert.False(fade.IsRemembered);
        Assert.Equal(0f, fade.Progress);
        Assert.False(fade.IsFading);
    }

    [Fact]
    public void ALayerInSightKeepsItsOwnColour()
    {
        var fade = new RememberedFade();

        var applied = fade.Applied(Base);

        Assert.Equal(Base.R, applied.R, 5);
        Assert.Equal(Base.G, applied.G, 5);
        Assert.Equal(Base.B, applied.B, 5);
    }

    [Fact]
    public void SnappingToRememberedArrivesWithNothingLeftToFade()
    {
        var fade = new RememberedFade();

        fade.Snap(true);

        Assert.True(fade.IsRemembered);
        Assert.Equal(1f, fade.Progress);
        Assert.False(fade.IsFading);
    }

    // A place already walked away from was never in sight to fade out of - it snaps straight to
    // fully in view, not to fully remembered.
    [Fact]
    public void SnappingToNotRememberedArrivesFullyInSight()
    {
        var fade = new RememberedFade();

        fade.Snap(false);

        Assert.False(fade.IsRemembered);
        Assert.Equal(0f, fade.Progress);
        Assert.False(fade.IsFading);
    }

    // Coming back into sight still has fading to do while any of the memory tint remains, not
    // only while fading further away from it.
    [Fact]
    public void ComingBackIntoSightStillHasFurtherToGoWhileTintRemains()
    {
        var fade = new RememberedFade();
        fade.Snap(true);

        fade.Retarget(false);

        Assert.True(fade.IsFading);
    }

    [Fact]
    public void AFullyRememberedLayerShowsTheTintMultipliedIntoItsOwnColour()
    {
        var fade = new RememberedFade();
        fade.Snap(true);

        var applied = fade.Applied(Base);

        Assert.Equal(Base.R * RememberedFade.Tint.R, applied.R, 5);
        Assert.Equal(Base.G * RememberedFade.Tint.G, applied.G, 5);
        Assert.Equal(Base.B * RememberedFade.Tint.B, applied.B, 5);
    }

    [Fact]
    public void HalfwayThroughTheFadeEachChannelIsHalfOfItsOwnDistanceToTheTint()
    {
        var fade = new RememberedFade();
        fade.Retarget(true);

        fade.Advance(RememberedFade.ToRememberedSeconds / 2f);
        var applied = fade.Applied(Base);

        Assert.Equal(0.5f, fade.Progress, 5);
        Assert.Equal(Base.R * (1f + RememberedFade.Tint.R) / 2f, applied.R, 5);
        Assert.Equal(Base.G * (1f + RememberedFade.Tint.G) / 2f, applied.G, 5);
        Assert.Equal(Base.B * (1f + RememberedFade.Tint.B) / 2f, applied.B, 5);
    }

    [Fact]
    public void TheAppliedColourCarriesTheBaseAlphaThrough()
    {
        var fade = new RememberedFade();
        fade.Snap(true);

        var applied = fade.Applied(new Color(Base.R, Base.G, Base.B, 0.35f));

        Assert.Equal(0.35f, applied.A, 5);
    }

    [Fact]
    public void RetargetingReportsOnlyAnActualChangeOfEndState()
    {
        var fade = new RememberedFade();

        // Exploration state is re-checked every tick, so most calls repeat what the fade already
        // knows and must cost nothing.
        Assert.False(fade.Retarget(false));
        Assert.True(fade.Retarget(true));
        Assert.False(fade.Retarget(true));
    }

    [Fact]
    public void RetargetingAloneMovesNothingYet()
    {
        var fade = new RememberedFade();

        fade.Retarget(true);

        Assert.Equal(0f, fade.Progress);
        Assert.True(fade.IsFading);
    }

    [Fact]
    public void AdvancingReportsThereIsStillFurtherToGo()
    {
        var fade = new RememberedFade();
        fade.Retarget(true);

        Assert.True(fade.Advance(RememberedFade.ToRememberedSeconds / 4f));
    }

    [Fact]
    public void AdvancingPastTheDurationStopsAtFullyRemembered()
    {
        var fade = new RememberedFade();
        fade.Retarget(true);

        var stillFading = fade.Advance(RememberedFade.ToRememberedSeconds * 3f);

        Assert.Equal(1f, fade.Progress);
        Assert.False(stillFading);
    }

    [Fact]
    public void ComingBackIntoSightStopsAtFullyVisible()
    {
        var fade = new RememberedFade();
        fade.Snap(true);
        fade.Retarget(false);

        var stillFading = fade.Advance(RememberedFade.ToVisibleSeconds * 3f);

        Assert.Equal(0f, fade.Progress);
        Assert.False(stillFading);
    }

    [Fact]
    public void ComingBackIntoSightIsQuickerThanFadingOutOfIt()
    {
        Assert.True(RememberedFade.ToVisibleSeconds < RememberedFade.ToRememberedSeconds);

        var fadingOut = new RememberedFade();
        fadingOut.Retarget(true);
        fadingOut.Advance(0.1f);

        var comingBack = new RememberedFade();
        comingBack.Snap(true);
        comingBack.Retarget(false);
        comingBack.Advance(0.1f);

        // The same tenth of a second covers more of the way back than it does of the way out.
        Assert.True(1f - comingBack.Progress > fadingOut.Progress);
    }

    [Fact]
    public void ReversingMidFadeContinuesFromWhereItGotTo()
    {
        var fade = new RememberedFade();
        fade.Retarget(true);
        fade.Advance(RememberedFade.ToRememberedSeconds / 2f);

        fade.Retarget(false);
        fade.Advance(RememberedFade.ToVisibleSeconds / 4f);

        // Halfway out, then a quarter of the way back from there: 0.5 - 0.25. Restarting from
        // the fully-remembered end would give 0.75 and flicker along the edge of sight.
        Assert.Equal(0.25f, fade.Progress, 5);
    }

    [Fact]
    public void AnArrivedFadeStaysWhereItIsWhenAdvancedAgain()
    {
        var fade = new RememberedFade();
        fade.Snap(true);

        var stillFading = fade.Advance(RememberedFade.ToRememberedSeconds);

        Assert.Equal(1f, fade.Progress);
        Assert.False(stillFading);
    }
}
