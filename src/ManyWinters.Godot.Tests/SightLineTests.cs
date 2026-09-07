using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// A camera at (-10, 6, -8) looking at a target at (14, 2, 10): deliberately oblique on all
// three axes, so a dropped component or a swapped sign cannot come out right by accident.
public class SightLineTests
{
    private static readonly Vector3 Camera = new(-10f, 6f, -8f);
    private static readonly Vector3 Target = new(14f, 2f, 10f);

    private static SightLine Line() => SightLine.From(Camera, Target)!.Value;

    // A point a given fraction of the way from the camera to the target, pushed `offset`
    // metres to one side of the line.
    private static Vector3 Beside(float fraction, float offset)
    {
        var line = Line();
        var sideways = line.Direction.Cross(Vector3.Up).Normalized();

        return line.Origin + (line.Direction * (line.Length * fraction)) + (sideways * offset);
    }

    [Fact]
    public void TheLineRunsFromTheCameraToTheTarget()
    {
        var line = Line();

        Assert.Equal(Camera, line.Origin);
        // sqrt(24^2 + 4^2 + 18^2) - no axis contributes the same amount as another.
        Assert.Equal(30.2654915f, line.Length, 4);
        Assert.Equal(1f, line.Direction.Length(), 4);
        Assert.Equal(Target, line.Origin + (line.Direction * line.Length));
    }

    [Fact]
    public void ACameraSittingOnItsOwnTargetHasNoLineToBlock()
    {
        Assert.Null(SightLine.From(Camera, Camera));
        Assert.Null(SightLine.From(Camera, Camera + new Vector3(0.0005f, 0f, 0f)));
    }

    [Fact]
    public void SomethingStandingOnTheLineHalfWayAlongBlocksIt()
    {
        Assert.True(Line().IsBlockedBy(Beside(0.5f, 0f), radius: 1f, margin: 0.5f, lengthTolerance: 1f));
    }

    [Fact]
    public void SomethingFarEnoughToOneSideDoesNot()
    {
        // Reach plus margin is 1.5m; three metres aside is clear of it.
        Assert.False(Line().IsBlockedBy(Beside(0.5f, 3f), radius: 1f, margin: 0.5f, lengthTolerance: 1f));
    }

    [Fact]
    public void AWiderThingBlocksFromFurtherOff()
    {
        // The whole reason the reach is a parameter: a broad canopy is in the way from much
        // further aside than a blade of grass at the same spot.
        var beside = Beside(0.5f, 2.5f);
        var line = Line();

        Assert.False(line.IsBlockedBy(beside, radius: 1f, margin: 0.5f, lengthTolerance: 1f));
        Assert.True(line.IsBlockedBy(beside, radius: 4f, margin: 0.5f, lengthTolerance: 1f));
    }

    [Fact]
    public void TheMarginWidensTheReachOnTopOfTheThingsOwnSize()
    {
        var beside = Beside(0.5f, 1.8f);
        var line = Line();

        Assert.False(line.IsBlockedBy(beside, radius: 1f, margin: 0.5f, lengthTolerance: 1f));
        Assert.True(line.IsBlockedBy(beside, radius: 1f, margin: 1.5f, lengthTolerance: 1f));
    }

    [Fact]
    public void SomethingBehindTheCameraIsNotInTheWay()
    {
        // Dead on the line, but the wrong side of the viewer.
        Assert.False(Line().IsBlockedBy(Beside(-0.3f, 0f), radius: 5f, margin: 1f, lengthTolerance: 1f));
    }

    [Fact]
    public void SomethingBeyondTheTargetIsNotInTheWayEither()
    {
        // Only what sits strictly between the two counts - a tree behind the person you are
        // watching is not blocking the view of them.
        Assert.False(Line().IsBlockedBy(Beside(1.5f, 0f), radius: 5f, margin: 1f, lengthTolerance: 1f));
    }

    [Fact]
    public void TheToleranceExtendsOnlyTheFarEndOfTheLine()
    {
        // Something level with the target, or a little past it, is not what the view needs
        // cleared - but the slack must not also reach back behind the camera.
        var line = Line();
        var justPastTarget = line.Origin + (line.Direction * (line.Length + 0.5f));
        var justBehindCamera = line.Origin - (line.Direction * 0.5f);

        Assert.True(line.IsBlockedBy(justPastTarget, radius: 1f, margin: 0.5f, lengthTolerance: 2f));
        Assert.False(line.IsBlockedBy(justPastTarget, radius: 1f, margin: 0.5f, lengthTolerance: 0f));
        Assert.False(line.IsBlockedBy(justBehindCamera, radius: 1f, margin: 0.5f, lengthTolerance: 2f));
    }

    [Fact]
    public void DistanceAlongTheLineIsNotConfusedWithDistanceAsideFromIt()
    {
        // A thing right next to the camera but far off to the side is close in a straight line
        // and nowhere near the view - measuring the wrong one of the two would fade it.
        var line = Line();
        var nearButAside = line.Origin + (line.Direction * 2f) + (line.Direction.Cross(Vector3.Up).Normalized() * 6f);

        Assert.False(line.IsBlockedBy(nearButAside, radius: 1f, margin: 0.5f, lengthTolerance: 1f));
    }

    [Fact]
    public void BlockingIsJudgedInThreeDimensionsNotJustOnTheGroundPlane()
    {
        // The camera looks down at the world, so "aside from the line" includes above and
        // below it. Something directly over the line by more than its reach is clear.
        var line = Line();
        var onLine = line.Origin + (line.Direction * (line.Length * 0.5f));

        Assert.True(line.IsBlockedBy(onLine + new Vector3(0f, 1f, 0f), radius: 2f, margin: 0f, lengthTolerance: 1f));
        Assert.False(line.IsBlockedBy(onLine + new Vector3(0f, 6f, 0f), radius: 2f, margin: 0f, lengthTolerance: 1f));
    }
}
