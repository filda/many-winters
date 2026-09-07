using Godot;

namespace ManyWinters.Godot.Tests;

// A sprite 2m wide and 4m tall standing at the origin, with the camera in front of it along
// +Z. Godot's Basis.Z points from the scene toward the viewer, so a camera looking down -Z has
// a backward axis of +Z; the pick ray then travels the other way.
public class BillboardUvTests
{
    private static readonly Vector3 SpriteCenter = Vector3.Zero;
    private const float HalfWidth = 1f;
    private const float HalfHeight = 2f;

    private static Vector2? UvOfRayThrough(Vector3 aim, Vector3 cameraBackward, bool flipH = false)
    {
        // A ray travelling toward the sprite from ten metres out, aimed at `aim`.
        var direction = -cameraBackward.Normalized();

        return BillboardUv.At(cameraBackward, aim - (direction * 10f), direction, SpriteCenter, HalfWidth, HalfHeight, flipH);
    }

    // An oblique ray: from `eye` through `aim`, the way a perspective camera casts every ray
    // that is not dead-centre on screen. Rays perpendicular to the plane are a special case
    // that hides errors in the intersection, because getting the crossing point wrong then
    // only moves it along the plane's own normal - which the UV ignores.
    private static Vector2? UvOfRayFrom(Vector3 eye, Vector3 aim, Vector3 cameraBackward, bool flipH = false) =>
        BillboardUv.At(cameraBackward, eye, (aim - eye).Normalized(), SpriteCenter, HalfWidth, HalfHeight, flipH);

    [Fact]
    public void AnObliqueRayCrossesThePlaneWhereItActuallyPointsAtIt()
    {
        // Half a metre right and a metre up on a 2x4m sprite: U three quarters across, V a
        // quarter down. The ray reaches it at an angle, so the crossing distance has to be
        // solved rather than assumed.
        var uv = UvOfRayFrom(new Vector3(0f, 0f, 10f), new Vector3(0.5f, 1f, 0f), Vector3.Back);

        Assert.Equal(0.75f, uv!.Value.X, 5);
        Assert.Equal(0.25f, uv.Value.Y, 5);
    }

    [Fact]
    public void ARayFromAnEyeOffToTheSideStillLandsWhereItPoints()
    {
        // The case the reconstruction used to get wrong for anything away from the screen's
        // centre (see SpritePixelHit's own doc comment) - the eye is nowhere near the sprite's
        // axis, so origin and direction both have to be honoured.
        var uv = UvOfRayFrom(new Vector3(-8f, 6f, 12f), new Vector3(-0.5f, -1f, 0f), Vector3.Back);

        Assert.Equal(0.25f, uv!.Value.X, 5);
        Assert.Equal(0.75f, uv.Value.Y, 5);
    }

    [Fact]
    public void ARayPointingPastTheSpriteMissesEvenThoughItCrossesThePlane()
    {
        // Crossing the plane is not hitting the sprite: this one passes well outside its
        // rectangle, which only the in-plane offsets can tell.
        Assert.Null(UvOfRayFrom(new Vector3(0f, 0f, 10f), new Vector3(4f, 0f, 0f), Vector3.Back));
    }

    [Fact]
    public void ARayThroughTheCentreLandsInTheMiddleOfTheTexture()
    {
        var uv = UvOfRayThrough(Vector3.Zero, Vector3.Back);

        Assert.Equal(0.5f, uv!.Value.X, 5);
        Assert.Equal(0.5f, uv.Value.Y, 5);
    }

    [Fact]
    public void MovingRightAcrossTheSpriteMovesUTowardOne()
    {
        // 0.9m right of centre on a 2m-wide sprite is 45% of the way from the middle to the
        // edge, so U lands at 0.95.
        var uv = UvOfRayThrough(new Vector3(0.9f, 0f, 0f), Vector3.Back);

        Assert.Equal(0.95f, uv!.Value.X, 5);
        Assert.Equal(0.5f, uv.Value.Y, 5);
    }

    [Fact]
    public void MovingUpTheSpriteMovesVTowardZeroBecauseImagesGrowDownward()
    {
        // 1m above centre on a 4m-tall sprite is a quarter of the height, and V counts from
        // the top - so up on screen is *down* in V.
        var uv = UvOfRayThrough(new Vector3(0f, 1f, 0f), Vector3.Back);

        Assert.Equal(0.5f, uv!.Value.X, 5);
        Assert.Equal(0.25f, uv.Value.Y, 5);
    }

    [Fact]
    public void TheFourCornersMapToTheFourCornersOfTheTexture()
    {
        Assert.Equal(new Vector2(0f, 0f), UvOfRayThrough(new Vector3(-HalfWidth, HalfHeight, 0f), Vector3.Back));
        Assert.Equal(new Vector2(1f, 0f), UvOfRayThrough(new Vector3(HalfWidth, HalfHeight, 0f), Vector3.Back));
        Assert.Equal(new Vector2(0f, 1f), UvOfRayThrough(new Vector3(-HalfWidth, -HalfHeight, 0f), Vector3.Back));
        Assert.Equal(new Vector2(1f, 1f), UvOfRayThrough(new Vector3(HalfWidth, -HalfHeight, 0f), Vector3.Back));
    }

    [Fact]
    public void CameraPitchDoesNotTiltThePlane()
    {
        // The whole point of FixedY billboarding: a sprite yaws to face the camera but never
        // leans back, so its base stays on the ground. Only the horizontal part of the
        // camera's backward axis may be used - a pitched camera looking at the same spot has
        // to sample the same pixel as a level one.
        var level = UvOfRayThrough(new Vector3(0.5f, 0.5f, 0f), Vector3.Back);
        var pitched = BillboardUv.At(
            new Vector3(0f, 0.7f, 0.7f),
            new Vector3(0.5f, 0.5f, 10f),
            Vector3.Forward,
            SpriteCenter,
            HalfWidth,
            HalfHeight,
            flipH: false);

        Assert.Equal(level!.Value.X, pitched!.Value.X, 5);
        Assert.Equal(level.Value.Y, pitched.Value.Y, 5);
    }

    [Fact]
    public void ThePlaneYawsToFaceTheCameraSoTheSpritesOwnRightFollowsIt()
    {
        // Seen from the east (+X), the sprite's own right-hand side points along -Z. Getting
        // this basis backwards is the classic silent failure: hover works dead-on and picks
        // the mirrored pixel from every other angle.
        var uv = UvOfRayThrough(new Vector3(0f, 0f, -0.9f), Vector3.Right);

        Assert.Equal(0.95f, uv!.Value.X, 5);
        Assert.Equal(0.5f, uv.Value.Y, 5);
    }

    [Fact]
    public void AHitBeyondTheSpritesEdgeIsNoHitAtAll()
    {
        Assert.Null(UvOfRayThrough(new Vector3(1.1f, 0f, 0f), Vector3.Back));
        Assert.Null(UvOfRayThrough(new Vector3(0f, 2.1f, 0f), Vector3.Back));
    }

    [Fact]
    public void ACameraLookingStraightDownHasNoYawLeftToFace()
    {
        // A FixedY billboard renders edge-on here, so there is no plane to intersect.
        Assert.Null(BillboardUv.At(Vector3.Up, new Vector3(0f, 10f, 0f), Vector3.Down, SpriteCenter, HalfWidth, HalfHeight, flipH: false));
    }

    [Fact]
    public void ARayRunningAlongThePlaneNeverCrossesIt()
    {
        Assert.Null(BillboardUv.At(Vector3.Back, new Vector3(-10f, 0f, 0f), Vector3.Right, SpriteCenter, HalfWidth, HalfHeight, flipH: false));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void ASpriteWithNoSizeCannotBeHit(float half)
    {
        Assert.Null(BillboardUv.At(Vector3.Back, new Vector3(0f, 0f, 10f), Vector3.Forward, SpriteCenter, half, HalfHeight, flipH: false));
        Assert.Null(BillboardUv.At(Vector3.Back, new Vector3(0f, 0f, 10f), Vector3.Forward, SpriteCenter, HalfWidth, half, flipH: false));
    }

    [Fact]
    public void FlipHMirrorsUAndLeavesVAlone()
    {
        // The render is mirrored without the node's transform changing, so the lookup has to
        // mirror too or it samples the wrong side of an asymmetric silhouette.
        var aim = new Vector3(0.9f, 1f, 0f);
        var plain = UvOfRayThrough(aim, Vector3.Back);

        var flipped = UvOfRayThrough(aim, Vector3.Back, flipH: true);

        Assert.Equal(1f - plain!.Value.X, flipped!.Value.X, 5);
        Assert.Equal(plain.Value.Y, flipped.Value.Y, 5);
    }

    [Fact]
    public void TheSpritesOwnPositionShiftsWhatCountsAsItsCentre()
    {
        // PersonView pins the test plane to a stable anchor rather than the sprite's own
        // position, which its walk bob nudges every frame - so the centre is a parameter, and
        // moving it has to move the sampled pixel with it.
        var center = new Vector3(5f, 3f, -2f);

        var uv = BillboardUv.At(Vector3.Back, center + new Vector3(0f, 0f, 10f), Vector3.Forward, center, HalfWidth, HalfHeight, flipH: false);

        Assert.Equal(0.5f, uv!.Value.X, 5);
        Assert.Equal(0.5f, uv.Value.Y, 5);
    }
}
