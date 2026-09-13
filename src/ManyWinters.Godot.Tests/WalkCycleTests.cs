using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

public class WalkCycleTests
{
    private const float Bob = 0.08f;

    [Fact]
    public void TheSpeedCoversTheDistanceInExactlyTheTimeGiven()
    {
        // Arriving as the next tick hands over a new target: too fast stutters, too slow lags.
        Assert.Equal(2.5f, WalkCycle.InterpolationSpeed(5f, 2f), 5);
    }

    [Fact]
    public void NoTimeAtAllMeansSnapRatherThanStandStill()
    {
        // What a corpse gets: it should stop where it fell, with nothing left to glide.
        Assert.Equal(float.MaxValue, WalkCycle.InterpolationSpeed(5f, 0f));
    }

    [Fact]
    public void AShorterDistanceInTheSameTimeIsSlower()
    {
        Assert.True(WalkCycle.InterpolationSpeed(2f, 1f) < WalkCycle.InterpolationSpeed(8f, 1f));
    }

    [Fact]
    public void SomeoneWithSomewhereToGoIsWalking()
    {
        Assert.True(WalkCycle.IsWalking(new Vector3(1f, 0f, 2f), new Vector3(4f, 0f, 6f)));
    }

    [Fact]
    public void SomeoneStandingOnTheirTargetIsNot()
    {
        var here = new Vector3(1f, 0f, 2f);

        Assert.False(WalkCycle.IsWalking(here, here));
        Assert.False(WalkCycle.IsWalking(here, here + new Vector3(0.0005f, 0f, 0f)));
    }

    [Fact]
    public void ArrivalIsJudgedInThreeDimensionsNotJustAlongOneAxis()
    {
        // A target directly above counts as somewhere to go, not as already there.
        var here = new Vector3(1f, 0f, 2f);

        Assert.True(WalkCycle.IsWalking(here, here + new Vector3(0f, 3f, 0f)));
    }

    [Fact]
    public void ThePhaseAdvancesByTheCycleRateOverTime()
    {
        // Ten cycles a second for a tenth of a second is one cycle's worth of phase.
        Assert.Equal(1f, WalkCycle.Advanced(0f, 0.1f, 10f), 5);
        Assert.Equal(3.5f, WalkCycle.Advanced(2.5f, 0.1f, 10f), 5);
    }

    [Fact]
    public void AFasterWalkerAdvancesFurtherInTheSameFrame()
    {
        Assert.True(WalkCycle.Advanced(0f, 0.016f, 8f) < WalkCycle.Advanced(0f, 0.016f, 12f));
    }

    [Fact]
    public void TheBobStartsNeutralAndRisesToItsAmplitude()
    {
        // Phase zero is the middle of the dip, a quarter turn is the top of it.
        Assert.Equal(0f, WalkCycle.BobAt(0f, Bob).Y, 5);
        Assert.Equal(Bob, WalkCycle.BobAt(MathF.PI / 2f, Bob).Y, 5);
        Assert.Equal(-Bob, WalkCycle.BobAt(3f * MathF.PI / 2f, Bob).Y, 5);
    }

    [Fact]
    public void TheBobMovesOnlyUpAndDown()
    {
        // Sideways or forward drift would slide the cutout off its own feet.
        var offset = WalkCycle.BobAt(1.3f, Bob);

        Assert.Equal(0f, offset.X, 5);
        Assert.Equal(0f, offset.Z, 5);
    }

    [Fact]
    public void TheAmplitudeScalesTheBob()
    {
        Assert.Equal(WalkCycle.BobAt(1.3f, Bob).Y * 2f, WalkCycle.BobAt(1.3f, Bob * 2f).Y, 5);
    }

    [Fact]
    public void TheBobRepeatsEveryFullTurnOfPhase()
    {
        Assert.Equal(WalkCycle.BobAt(0.7f, Bob).Y, WalkCycle.BobAt(0.7f + (2f * MathF.PI), Bob).Y, 3);
    }
}
