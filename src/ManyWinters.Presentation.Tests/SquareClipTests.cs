using Godot;
using ManyWinters.Presentation.Logic;

namespace ManyWinters.Presentation.Tests;

// The square spans -50..50 both ways. Segments are oblique, so a mixed-up axis or edge cannot
// come out right by accident.
public class SquareClipTests
{
    private const float Half = 50f;

    [Fact]
    public void ASegmentWhollyInsideIsKeptAsItIs()
    {
        var clipped = SquareClip.Clip(new Vector2(-12f, 7f), new Vector2(31f, -22f), Half);

        Assert.Equal((new Vector2(-12f, 7f), new Vector2(31f, -22f)), clipped);
    }

    [Fact]
    public void ASegmentLeavingThroughTheRightEdgeEndsOnIt()
    {
        // From (20, 10) toward (80, 40): x = 50 at t = 0.5, where y = 25.
        var (start, end) = SquareClip.Clip(new Vector2(20f, 10f), new Vector2(80f, 40f), Half)!.Value;

        AssertClose(new Vector2(20f, 10f), start);
        AssertClose(new Vector2(50f, 25f), end);
    }

    [Fact]
    public void ASegmentEnteringThroughTheBottomEdgeStartsOnIt()
    {
        // From (-30, -90) toward (-10, -10): y = -50 at t = 0.5, where x = -20.
        var (start, end) = SquareClip.Clip(new Vector2(-30f, -90f), new Vector2(-10f, -10f), Half)!.Value;

        AssertClose(new Vector2(-20f, -50f), start);
        AssertClose(new Vector2(-10f, -10f), end);
    }

    [Fact]
    public void ASegmentCrossingTheWholeSquareIsCutAtBothEnds()
    {
        // From (-70, -20) toward (80, 40): x = -50 at t = 2/15 (y = -12), x = 50 at t = 0.8 (y = 28).
        var (start, end) = SquareClip.Clip(new Vector2(-70f, -20f), new Vector2(80f, 40f), Half)!.Value;

        AssertClose(new Vector2(-50f, -12f), start);
        AssertClose(new Vector2(50f, 28f), end);
    }

    [Fact]
    public void ASegmentPassingBesideACornerIsDropped()
    {
        Assert.Null(SquareClip.Clip(new Vector2(30f, 90f), new Vector2(90f, 30f), Half));
    }

    [Fact]
    public void ASegmentWhollyOutsideOnOneSideIsDropped()
    {
        Assert.Null(SquareClip.Clip(new Vector2(-60f, -45f), new Vector2(-95f, 20f), Half));
    }

    [Fact]
    public void AnAxisAlignedSegmentOutsideTheSquareIsDropped()
    {
        Assert.Null(SquareClip.Clip(new Vector2(-30f, 61f), new Vector2(40f, 61f), Half));
    }

    [Fact]
    public void AnAxisAlignedSegmentInsideTheSquareIsCutOnlyAlongItsLength()
    {
        var (start, end) = SquareClip.Clip(new Vector2(-30f, 61f), new Vector2(-30f, -12f), Half)!.Value;

        AssertClose(new Vector2(-30f, 50f), start);
        AssertClose(new Vector2(-30f, -12f), end);
    }

    private static void AssertClose(Vector2 expected, Vector2 actual)
    {
        Assert.Equal(expected.X, actual.X, 0.001f);
        Assert.Equal(expected.Y, actual.Y, 0.001f);
    }
}
