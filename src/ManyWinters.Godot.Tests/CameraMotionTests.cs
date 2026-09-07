using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

public class CameraMotionTests
{
    private const float Rate = 2.5f;
    private const float MinZoom = 3f;
    private const float MaxZoom = 2000f;

    [Fact]
    public void HoldingZoomForASecondMultipliesTheDistanceByTheRate()
    {
        // Multiplicative, not additive: a second of zoom out is 2.5x further away, wherever it
        // started. Ten metres becomes twenty-five, not twelve and a half.
        Assert.Equal(25f, CameraMotion.Zoomed(10f, 1f, Rate, MinZoom, MaxZoom), 4);
    }

    [Fact]
    public void ZoomingInIsTheExactInverseOfZoomingOut()
    {
        // A second out then a second in has to land back where it started, or repeated
        // wheel-up/wheel-down would drift.
        var out1 = CameraMotion.Zoomed(10f, 1f, Rate, MinZoom, MaxZoom);

        Assert.Equal(10f, CameraMotion.Zoomed(out1, -1f, Rate, MinZoom, MaxZoom), 4);
    }

    [Fact]
    public void NoTimeHeldMeansNoZoom()
    {
        Assert.Equal(10f, CameraMotion.Zoomed(10f, 0f, Rate, MinZoom, MaxZoom), 5);
    }

    [Fact]
    public void ZoomStopsAtBothEndsOfItsRange()
    {
        Assert.Equal(MaxZoom, CameraMotion.Zoomed(1900f, 1f, Rate, MinZoom, MaxZoom), 4);
        Assert.Equal(MinZoom, CameraMotion.Zoomed(4f, -1f, Rate, MinZoom, MaxZoom), 4);
    }

    [Fact]
    public void ASmallStepZoomsBySmallerThanTheFullRate()
    {
        // A mouse notch is a fraction of a second's worth of the held-key rate, so it lands
        // between "no change" and a full second of zoom.
        var notch = CameraMotion.Zoomed(10f, 0.05f, Rate, MinZoom, MaxZoom);

        Assert.True(notch > 10f);
        Assert.True(notch < 25f);
    }

    [Fact]
    public void TiltAddsItsStepAndStopsAtEitherLimit()
    {
        Assert.Equal(35f, CameraMotion.Tilted(20f, 15f, 12f, 70f), 5);
        Assert.Equal(70f, CameraMotion.Tilted(65f, 15f, 12f, 70f), 5);
        Assert.Equal(12f, CameraMotion.Tilted(15f, -10f, 12f, 70f), 5);
    }

    [Fact]
    public void TheOffsetDirectionSplitsIntoHeightAndDistanceByTheTiltAngle()
    {
        // Thirty degrees deliberately, not forty-five: there sine and cosine are equal, so
        // swapping the two components would go unnoticed.
        var direction = CameraMotion.OffsetDirection(30f);

        Assert.Equal(0f, direction.X, 5);
        Assert.Equal(0.5f, direction.Y, 5);
        Assert.Equal(0.8660254f, direction.Z, 5);
    }

    [Fact]
    public void TheOffsetDirectionIsAlwaysAUnitVectorSoZoomAloneSetsTheDistance()
    {
        for (var tilt = 12f; tilt <= 70f; tilt += 2f)
        {
            Assert.Equal(1f, CameraMotion.OffsetDirection(tilt).Length(), 4);
        }
    }

    [Fact]
    public void ALowerTiltSitsLowerAndFurtherBack()
    {
        // Height and horizontal distance come off the same angle, so they trade against each
        // other - dropping the tilt has to do both at once, not just one.
        var low = CameraMotion.OffsetDirection(20f);
        var high = CameraMotion.OffsetDirection(60f);

        Assert.True(low.Y < high.Y);
        Assert.True(low.Z > high.Z);
    }

    [Fact]
    public void PanRunsAlongTheRigsOwnAxes()
    {
        // Unrotated: X input goes east, Y input goes along the rig's forward axis.
        Assert.Equal(new Vector3(10f, 0f, 0f), CameraMotion.PanVelocity(Basis.Identity, new Vector2(1f, 0f), 10f));
        Assert.Equal(new Vector3(0f, 0f, 10f), CameraMotion.PanVelocity(Basis.Identity, new Vector2(0f, 1f), 10f));
    }

    [Fact]
    public void PanFollowsTheRigWhenItHasBeenRotated()
    {
        // The point of using the rig's basis at all: after turning the view a quarter turn,
        // "forward" is a different world direction, and panning has to follow the view rather
        // than the world's axes.
        var turned = new Basis(Vector3.Up, Mathf.DegToRad(90f));

        var velocity = CameraMotion.PanVelocity(turned, new Vector2(0f, 1f), 10f);

        Assert.Equal(10f, velocity.X, 4);
        Assert.Equal(0f, velocity.Z, 4);
    }

    [Fact]
    public void PanningDiagonallyIsNoFasterThanPanningStraight()
    {
        var straight = CameraMotion.PanVelocity(Basis.Identity, new Vector2(1f, 0f), 10f);
        var diagonal = CameraMotion.PanVelocity(Basis.Identity, new Vector2(1f, 1f), 10f);

        Assert.Equal(straight.Length(), diagonal.Length(), 4);
    }

    [Fact]
    public void PanStaysHorizontalHoweverTheRigIsTilted()
    {
        // The rig's own axes are flattened first, so panning never lifts or sinks the view.
        var pitched = new Basis(Vector3.Right, Mathf.DegToRad(40f));

        var velocity = CameraMotion.PanVelocity(pitched, new Vector2(0.3f, 1f), 10f);

        Assert.Equal(0f, velocity.Y, 4);
    }

    [Fact]
    public void NoInputMeansNoPan()
    {
        Assert.Equal(Vector3.Zero, CameraMotion.PanVelocity(Basis.Identity, Vector2.Zero, 10f));
    }

    [Fact]
    public void PanSpeedScalesWithWhatItIsGiven()
    {
        var slow = CameraMotion.PanVelocity(Basis.Identity, new Vector2(1f, 0f), 4f);
        var fast = CameraMotion.PanVelocity(Basis.Identity, new Vector2(1f, 0f), 12f);

        Assert.Equal(slow.Length() * 3f, fast.Length(), 4);
    }

    [Fact]
    public void EasingClosesAboutTwoThirdsOfTheGapInOneTimeConstant()
    {
        // 1/easeRate is the time constant, so a tenth of a second at rate 10 should close
        // 1 - 1/e of the way - the property the comment on the constant claims.
        var eased = CameraMotion.Eased(Vector3.Zero, new Vector3(10f, 0f, 0f), 10f, 0.1f);

        Assert.Equal(6.3212056f, eased.X, 4);
    }

    [Fact]
    public void EasingApproachesTheTargetWithoutOvershootingIt()
    {
        var velocity = Vector3.Zero;
        for (var step = 0; step < 200; step++)
        {
            velocity = CameraMotion.Eased(velocity, new Vector3(10f, 0f, 0f), 10f, 0.016f);
            Assert.InRange(velocity.X, 0f, 10f);
        }

        Assert.Equal(10f, velocity.X, 3);
    }

    [Fact]
    public void EasingWithNoTimePassedChangesNothing()
    {
        var current = new Vector3(3f, 0f, 4f);

        Assert.Equal(current, CameraMotion.Eased(current, new Vector3(10f, 0f, 0f), 10f, 0f));
    }

    [Fact]
    public void ACameraAlreadyClearOfTheGroundIsLeftWhereItIs()
    {
        Assert.Equal(5f, CameraMotion.ClearedHeight(5f, 2f, 0.3f), 5);
    }

    [Fact]
    public void ACameraInsideABumpIsLiftedToTheClearanceAboveIt()
    {
        // Only a little: the point is to stop the view ending up under the terrain, not to
        // shove the camera away from it.
        Assert.Equal(2.3f, CameraMotion.ClearedHeight(2.1f, 2f, 0.3f), 5);
    }

    [Fact]
    public void ClearanceIsMeasuredFromTheGroundNotFromZero()
    {
        // A camera below sea level over ground below sea level is still fine.
        Assert.Equal(-4.7f, CameraMotion.ClearedHeight(-6f, -5f, 0.3f), 5);
    }
}
