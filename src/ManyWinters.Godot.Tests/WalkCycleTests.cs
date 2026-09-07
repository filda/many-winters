using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

public class WalkCycleTests
{
    private const float Bob = 0.08f;
    private const float Rock = 0.12f;

    [Fact]
    public void TheSpeedCoversTheDistanceInExactlyTheTimeGiven()
    {
        // Arriving as the next tick hands over a new target is the point: too fast stutters,
        // too slow lags a step behind the simulation.
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
    public void ThePoseStartsNeutralAndRisesToTheBobAmplitude()
    {
        // Phase zero is the middle of the dip, a quarter turn is the top of it.
        Assert.Equal(0f, WalkCycle.PoseAt(0f, Bob, Rock).Offset.Y, 5);
        Assert.Equal(Bob, WalkCycle.PoseAt(MathF.PI / 2f, Bob, Rock).Offset.Y, 5);
        Assert.Equal(-Bob, WalkCycle.PoseAt(3f * MathF.PI / 2f, Bob, Rock).Offset.Y, 5);
    }

    [Fact]
    public void TheBobMovesOnlyUpAndDown()
    {
        // Sideways or forward drift would slide the cutout off its own feet.
        var pose = WalkCycle.PoseAt(1.3f, Bob, Rock);

        Assert.Equal(0f, pose.Offset.X, 5);
        Assert.Equal(0f, pose.Offset.Z, 5);
    }

    [Fact]
    public void TheRockLeansAboutTheLocalZAxisAlone()
    {
        // Any other axis would turn the cutout away from the camera and break the billboard.
        var pose = WalkCycle.PoseAt(1.3f, Bob, Rock);

        Assert.Equal(0f, pose.Rotation.X, 5);
        Assert.Equal(0f, pose.Rotation.Y, 5);
        Assert.NotEqual(0f, pose.Rotation.Z);
    }

    [Fact]
    public void TheRockRunsAtHalfTheBobsFrequencySoItIsOnePerStride()
    {
        // A body dips once per footfall but leans over once per stride. At a full bob cycle
        // the bob is back where it started while the rock is only half way round - at its own
        // extreme. In step with the bob it would read as a limp.
        var full = WalkCycle.PoseAt(2f * MathF.PI, Bob, Rock);

        Assert.Equal(0f, full.Offset.Y, 4);
        Assert.Equal(0f, MathF.Abs(full.Rotation.Z), 4);

        var half = WalkCycle.PoseAt(MathF.PI, Bob, Rock);

        Assert.Equal(0f, half.Offset.Y, 4);
        Assert.Equal(Rock, half.Rotation.Z, 4);
    }

    [Fact]
    public void BothAmplitudesScaleTheirOwnPartAndNotTheOther()
    {
        var normal = WalkCycle.PoseAt(1.3f, Bob, Rock);
        var bobbier = WalkCycle.PoseAt(1.3f, Bob * 2f, Rock);
        var rockier = WalkCycle.PoseAt(1.3f, Bob, Rock * 2f);

        Assert.Equal(normal.Offset.Y * 2f, bobbier.Offset.Y, 5);
        Assert.Equal(normal.Rotation.Z, bobbier.Rotation.Z, 5);
        Assert.Equal(normal.Rotation.Z * 2f, rockier.Rotation.Z, 5);
        Assert.Equal(normal.Offset.Y, rockier.Offset.Y, 5);
    }

    [Fact]
    public void ThePoseRepeatsEveryTwoFullTurnsOfPhase()
    {
        // The rock's half frequency makes the combined cycle twice as long as the bob's, so
        // this is where the whole pose actually comes back round.
        var start = WalkCycle.PoseAt(0.7f, Bob, Rock);
        var later = WalkCycle.PoseAt(0.7f + (4f * MathF.PI), Bob, Rock);

        Assert.Equal(start.Offset.Y, later.Offset.Y, 3);
        Assert.Equal(start.Rotation.Z, later.Rotation.Z, 3);
    }
}
