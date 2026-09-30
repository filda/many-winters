using Godot;
using ManyWinters.Presentation.Logic;

namespace ManyWinters.Presentation.Tests;

public class RememberedFadeTests
{
    // Base modulate is never plain white in practice, and each channel here is a different
    // distance from 1, so a tint on the wrong channel or dropped entirely cannot pass.
    private static readonly Color Base = new(0.9f, 0.6f, 0.4f);

    private static readonly PresentationSettings Settings = PresentationSettings.Default;
    private static readonly Color Tint = RememberedFade.Tint;
    private static readonly float ToRememberedSeconds = Settings.FadeToRememberedSeconds;
    private static readonly float ToVisibleSeconds = Settings.FadeToVisibleSeconds;

    [Fact]
    public void AFreshFadeIsInSightAndNotMoving()
    {
        var fade = new RememberedFade(Settings);

        Assert.False(fade.IsRemembered);
        Assert.Equal(0f, fade.Progress);
        Assert.False(fade.IsFading);
    }

    [Fact]
    public void ALayerInSightKeepsItsOwnColour()
    {
        var fade = new RememberedFade(Settings);

        var applied = fade.Applied(Base);

        Assert.Equal(Base.R, applied.R, 5);
        Assert.Equal(Base.G, applied.G, 5);
        Assert.Equal(Base.B, applied.B, 5);
    }

    [Fact]
    public void SnappingToRememberedArrivesWithNothingLeftToFade()
    {
        var fade = new RememberedFade(Settings);

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
        var fade = new RememberedFade(Settings);

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
        var fade = new RememberedFade(Settings);
        fade.Snap(true);

        fade.Retarget(false);

        Assert.True(fade.IsFading);
    }

    [Fact]
    public void AFullyRememberedLayerShowsTheTintMultipliedIntoItsOwnColour()
    {
        var fade = new RememberedFade(Settings);
        fade.Snap(true);

        var applied = fade.Applied(Base);

        Assert.Equal(Base.R * Tint.R, applied.R, 5);
        Assert.Equal(Base.G * Tint.G, applied.G, 5);
        Assert.Equal(Base.B * Tint.B, applied.B, 5);
    }

    [Fact]
    public void HalfwayThroughTheFadeEachChannelIsHalfOfItsOwnDistanceToTheTint()
    {
        var fade = new RememberedFade(Settings);
        fade.Retarget(true);

        fade.Advance(ToRememberedSeconds / 2f);
        var applied = fade.Applied(Base);

        Assert.Equal(0.5f, fade.Progress, 5);
        Assert.Equal(Base.R * (1f + Tint.R) / 2f, applied.R, 5);
        Assert.Equal(Base.G * (1f + Tint.G) / 2f, applied.G, 5);
        Assert.Equal(Base.B * (1f + Tint.B) / 2f, applied.B, 5);
    }

    [Fact]
    public void TheAppliedColourCarriesTheBaseAlphaThrough()
    {
        var fade = new RememberedFade(Settings);
        fade.Snap(true);

        var applied = fade.Applied(new Color(Base.R, Base.G, Base.B, 0.35f));

        Assert.Equal(0.35f, applied.A, 5);
    }

    [Fact]
    public void RetargetingReportsOnlyAnActualChangeOfEndState()
    {
        var fade = new RememberedFade(Settings);

        // Exploration state is re-checked every tick, so most calls repeat what the fade already
        // knows and must cost nothing.
        Assert.False(fade.Retarget(false));
        Assert.True(fade.Retarget(true));
        Assert.False(fade.Retarget(true));
    }

    [Fact]
    public void RetargetingAloneMovesNothingYet()
    {
        var fade = new RememberedFade(Settings);

        fade.Retarget(true);

        Assert.Equal(0f, fade.Progress);
        Assert.True(fade.IsFading);
    }

    [Fact]
    public void AdvancingReportsThereIsStillFurtherToGo()
    {
        var fade = new RememberedFade(Settings);
        fade.Retarget(true);

        Assert.True(fade.Advance(ToRememberedSeconds / 4f));
    }

    [Fact]
    public void AdvancingPastTheDurationStopsAtFullyRemembered()
    {
        var fade = new RememberedFade(Settings);
        fade.Retarget(true);

        var stillFading = fade.Advance(ToRememberedSeconds * 3f);

        Assert.Equal(1f, fade.Progress);
        Assert.False(stillFading);
    }

    [Fact]
    public void ComingBackIntoSightStopsAtFullyVisible()
    {
        var fade = new RememberedFade(Settings);
        fade.Snap(true);
        fade.Retarget(false);

        var stillFading = fade.Advance(ToVisibleSeconds * 3f);

        Assert.Equal(0f, fade.Progress);
        Assert.False(stillFading);
    }

    [Fact]
    public void ComingBackIntoSightIsQuickerThanFadingOutOfIt()
    {
        Assert.True(ToVisibleSeconds < ToRememberedSeconds);

        var fadingOut = new RememberedFade(Settings);
        fadingOut.Retarget(true);
        fadingOut.Advance(0.1f);

        var comingBack = new RememberedFade(Settings);
        comingBack.Snap(true);
        comingBack.Retarget(false);
        comingBack.Advance(0.1f);

        // The same tenth of a second covers more of the way back than it does of the way out.
        Assert.True(1f - comingBack.Progress > fadingOut.Progress);
    }

    [Fact]
    public void ReversingMidFadeContinuesFromWhereItGotTo()
    {
        var fade = new RememberedFade(Settings);
        fade.Retarget(true);
        fade.Advance(ToRememberedSeconds / 2f);

        fade.Retarget(false);
        fade.Advance(ToVisibleSeconds / 4f);

        // Halfway out, then a quarter of the way back from there: 0.5 - 0.25. Restarting from
        // the fully-remembered end would give 0.75 and flicker along the edge of sight.
        Assert.Equal(0.25f, fade.Progress, 5);
    }

    [Fact]
    public void AnArrivedFadeStaysWhereItIsWhenAdvancedAgain()
    {
        var fade = new RememberedFade(Settings);
        fade.Snap(true);

        var stillFading = fade.Advance(ToRememberedSeconds);

        Assert.Equal(1f, fade.Progress);
        Assert.False(stillFading);
    }
}
