using Godot;

namespace ManyWinters.Godot.Tests;

// A sprite 2m wide and 4m tall standing at the origin, with the camera in front of it along
// +Z. Godot's Basis.Z points from the scene toward the viewer, so a camera looking down -Z has
// a backward axis of +Z; the pick ray then travels the other way.
public class BillboardUvTests
{
    private static readonly Vector3 SpriteCenter = Vector3.Zero;
    private const float Width = 2f;
    private const float Height = 4f;
    private const float HalfWidth = Width / 2f;
    private const float HalfHeight = Height / 2f;

    // A ray travelling straight at the sprite from ten metres out, aimed at `aim` - the
    // perpendicular special case of UvOfRayFrom below.
    private static Vector2? UvOfRayThrough(Vector3 aim, Vector3 cameraBackward, bool flipH = false) =>
        UvOfRayFrom(aim + (cameraBackward.Normalized() * 10f), aim, cameraBackward, flipH);

    // An oblique ray: from `eye` through `aim`, the way a perspective camera casts every ray
    // that is not dead-centre on screen. Rays perpendicular to the plane are a special case
    // that hides errors in the intersection, because getting the crossing point wrong then
    // only moves it along the plane's own normal - which the UV ignores.
    private static Vector2? UvOfRayFrom(Vector3 eye, Vector3 aim, Vector3 cameraBackward, bool flipH = false) =>
        BillboardUv.At(cameraBackward, eye, (aim - eye).Normalized(), SpriteCenter, Width, Height, flipH);

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
        var pitched = UvOfRayFrom(new Vector3(0.5f, 0.5f, 10f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0f, 0.7f, 0.7f));

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
        Assert.Null(UvOfRayFrom(new Vector3(0f, 10f, 0f), Vector3.Zero, Vector3.Up));
    }

    [Fact]
    public void ARayRunningAlongThePlaneNeverCrossesIt()
    {
        Assert.Null(BillboardUv.At(Vector3.Back, new Vector3(-10f, 0f, 0f), Vector3.Right, SpriteCenter, Width, Height, flipH: false));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void ASpriteWithNoSizeCannotBeHit(float size)
    {
        Assert.Null(BillboardUv.At(Vector3.Back, new Vector3(0f, 0f, 10f), Vector3.Forward, SpriteCenter, size, Height, flipH: false));
        Assert.Null(BillboardUv.At(Vector3.Back, new Vector3(0f, 0f, 10f), Vector3.Forward, SpriteCenter, Width, size, flipH: false));
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

        var uv = BillboardUv.At(Vector3.Back, center + new Vector3(0f, 0f, 10f), Vector3.Forward, center, Width, Height, flipH: false);

        Assert.Equal(0.5f, uv!.Value.X, 5);
        Assert.Equal(0.5f, uv.Value.Y, 5);
    }

    [Fact]
    public void RenderedSizeIsTheTexturesPixelsTurnedIntoMetres()
    {
        // A 100x200 texture at a centimetre per pixel, unscaled: one metre by two.
        var size = BillboardUv.RenderedSize(pixelSize: 0.01f, textureWidth: 100, textureHeight: 200, scaleX: 1f, scaleY: 1f);

        Assert.Equal(1f, size.X, 5);
        Assert.Equal(2f, size.Y, 5);
    }

    [Fact]
    public void EachAxisTakesItsOwnScale()
    {
        // The case per-axis scaling exists for: a tree stretched tall and squeezed narrow. A
        // single shared scale factor would give a square sprite here and pick the wrong pixel
        // on every non-square one.
        var size = BillboardUv.RenderedSize(pixelSize: 0.01f, textureWidth: 100, textureHeight: 100, scaleX: 0.5f, scaleY: 3f);

        Assert.Equal(0.5f, size.X, 5);
        Assert.Equal(3f, size.Y, 5);
    }

    [Fact]
    public void APixelSizeThatIgnoredScaleWouldMisreadAStretchedSprite()
    {
        // Guards the multiplication itself: at scale 2 the sprite covers twice the ground, so
        // a ray landing 0.9m out is only 45% of the way to the edge, not past it.
        var unscaled = BillboardUv.RenderedSize(0.01f, 100, 100, 1f, 1f);
        var doubled = BillboardUv.RenderedSize(0.01f, 100, 100, 2f, 2f);

        Assert.Equal(unscaled.X * 2f, doubled.X, 5);
        Assert.Equal(unscaled.Y * 2f, doubled.Y, 5);
    }

    [Theory]
    [InlineData(0f, 0f, 0, 0)]
    [InlineData(0.5f, 0.5f, 50, 100)]
    [InlineData(0.999f, 0.999f, 99, 199)]
    public void PixelAtTruncatesTowardTheTopLeftOfTheTexel(float u, float v, int expectedX, int expectedY)
    {
        Assert.Equal((expectedX, expectedY), BillboardUv.PixelAt(new Vector2(u, v), width: 100, height: 200));
    }

    [Fact]
    public void PixelAtKeepsTheFarEdgeInsideTheTexture()
    {
        // A ray through the sprite's own corner really does produce a UV of exactly 1, which
        // truncates to the width itself - one past the last pixel. Out of bounds when sampled,
        // and a one-pixel-wrong hover if it merely wrapped.
        Assert.Equal((99, 199), BillboardUv.PixelAt(new Vector2(1f, 1f), width: 100, height: 200));
    }

    [Fact]
    public void PixelAtRefusesToRunOffTheNearEdgeEither()
    {
        Assert.Equal((0, 0), BillboardUv.PixelAt(new Vector2(-0.5f, -0.5f), width: 100, height: 200));
    }
}
