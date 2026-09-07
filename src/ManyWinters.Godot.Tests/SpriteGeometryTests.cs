using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

public class SpriteGeometryTests
{
    private static SpriteExtents.Extent Extent(float width, float height, float centerX, float centerY) =>
        new(width, height, centerX, centerY);

    [Fact]
    public void CombiningTwoStackedExtentsSpansBothOfThem()
    {
        // A split tree: a trunk low and narrow, a canopy high and wide. Their union is what the
        // whole tree's silhouette used to be before the sprite was cut in two, so the combined
        // extent has to reach from the trunk's foot to the canopy's crown.
        var trunk = Extent(width: 1f, height: 2f, centerX: 0f, centerY: 1f);
        var canopy = Extent(width: 4f, height: 3f, centerX: 0f, centerY: 3.5f);

        var combined = SpriteExtents.Combine(trunk, canopy);

        Assert.Equal(4f, combined.Width, 5);
        Assert.Equal(5f, combined.Height, 5);
        Assert.Equal(0f, combined.CenterXOffset, 5);
        Assert.Equal(2.5f, combined.CenterYOffset, 5);
    }

    [Fact]
    public void CombiningKeepsAnOffCentreLayersOwnReach()
    {
        // Neither layer is centred on the node's origin, and they lean opposite ways - the
        // union has to reach the outer edge of each rather than averaging them.
        var left = Extent(width: 2f, height: 1f, centerX: -3f, centerY: 0f);
        var right = Extent(width: 2f, height: 1f, centerX: 3f, centerY: 0f);

        var combined = SpriteExtents.Combine(left, right);

        Assert.Equal(8f, combined.Width, 5);
        Assert.Equal(0f, combined.CenterXOffset, 5);
    }

    [Fact]
    public void CombiningAnExtentWithItselfChangesNothing()
    {
        var only = Extent(width: 2f, height: 3f, centerX: 0.5f, centerY: -1f);

        var combined = SpriteExtents.Combine(only, only);

        Assert.Equal(only, combined);
    }

    [Fact]
    public void CombiningIsOrderIndependent()
    {
        var trunk = Extent(width: 1f, height: 2f, centerX: 0.2f, centerY: 1f);
        var canopy = Extent(width: 4f, height: 3f, centerX: -0.1f, centerY: 3.5f);

        Assert.Equal(
            SpriteExtents.Combine(trunk, canopy),
            SpriteExtents.Combine(canopy, trunk));
    }

    [Fact]
    public void ModulateUndoesTheRecolourableBaseSoTheAskedForColourComesOutOfTheShader()
    {
        // The person sprite is drawn in a near-white base tint so it can be recoloured; the
        // modulate has to divide that base back out, or every requested colour renders darker
        // than it was asked for. Feeding the base itself back in must therefore give plain
        // white - no tint at all.
        var neutral = new Color(0.82f, 0.80f, 0.78f);

        var modulate = SpriteTint.ModulateFor(neutral);

        Assert.Equal(1f, modulate.R, 5);
        Assert.Equal(1f, modulate.G, 5);
        Assert.Equal(1f, modulate.B, 5);
    }

    [Fact]
    public void ModulateScalesEachChannelByItsOwnShareOfTheBase()
    {
        var modulate = SpriteTint.ModulateFor(new Color(0.41f, 0.40f, 0.39f));

        // Exactly half of the base on every channel, so exactly half the modulate.
        Assert.Equal(0.5f, modulate.R, 5);
        Assert.Equal(0.5f, modulate.G, 5);
        Assert.Equal(0.5f, modulate.B, 5);
    }

    // A 100x200 canvas standing 4m tall, so 2cm of world per pixel. The used rect is
    // deliberately off-centre on both axes and not square: a swapped axis or a lost sign
    // survives anything symmetric.
    private static readonly Vector2 Canvas = new(100f, 200f);
    private const float WorldHeight = 4f;

    [Fact]
    public void ContentFillingTheWholeCanvasIsTheFullWorldSizeAndSitsOnTheOrigin()
    {
        var extent = SpriteExtents.From(Vector2.Zero, Canvas, Canvas, WorldHeight);

        Assert.Equal(2f, extent.Width, 5);
        Assert.Equal(4f, extent.Height, 5);
        Assert.Equal(0f, extent.CenterXOffset, 5);
        Assert.Equal(0f, extent.CenterYOffset, 5);
    }

    [Fact]
    public void ContentIsMeasuredInWorldMetresNotPixels()
    {
        // Half the canvas wide and a quarter of it tall, at 2cm per pixel.
        var extent = SpriteExtents.From(Vector2.Zero, new Vector2(50f, 50f), Canvas, WorldHeight);

        Assert.Equal(1f, extent.Width, 5);
        Assert.Equal(1f, extent.Height, 5);
    }

    [Fact]
    public void ContentToTheRightOfTheCanvasCentreOffsetsPositively()
    {
        // A 20px block whose centre sits 30px right of the canvas centre: +0.6m.
        var extent = SpriteExtents.From(new Vector2(70f, 90f), new Vector2(20f, 20f), Canvas, WorldHeight);

        Assert.Equal(0.6f, extent.CenterXOffset, 5);
    }

    [Fact]
    public void ContentHighOnTheCanvasOffsetsUpwardBecauseImageRowsCountDownward()
    {
        // Rows 10..30 are near the *top* of the image, which is up in the world - the sign has
        // to flip on this axis and not on the other.
        var extent = SpriteExtents.From(new Vector2(40f, 10f), new Vector2(20f, 20f), Canvas, WorldHeight);

        Assert.True(extent.CenterYOffset > 0f, "content near the top of the image sits above the origin");
        Assert.Equal(1.6f, extent.CenterYOffset, 5);
    }

    [Fact]
    public void ContentLowOnTheCanvasOffsetsDownward()
    {
        var extent = SpriteExtents.From(new Vector2(40f, 170f), new Vector2(20f, 20f), Canvas, WorldHeight);

        Assert.Equal(-1.6f, extent.CenterYOffset, 5);
    }

    [Fact]
    public void TheTwoAxesAreNotInterchangeable()
    {
        // Same rect, transposed. If width and height or the two offsets were ever swapped, one
        // of these would come out as the other.
        var wide = SpriteExtents.From(new Vector2(10f, 60f), new Vector2(80f, 20f), Canvas, WorldHeight);
        var tall = SpriteExtents.From(new Vector2(60f, 10f), new Vector2(20f, 80f), Canvas, WorldHeight);

        Assert.Equal(1.6f, wide.Width, 5);
        Assert.Equal(0.4f, wide.Height, 5);
        Assert.Equal(0.4f, tall.Width, 5);
        Assert.Equal(1.6f, tall.Height, 5);
    }

    [Fact]
    public void AShorterSpriteScalesEverythingWithIt()
    {
        // The same texture placed at half the world height is half the extent, offsets too -
        // the whole thing hangs off worldHeight over the canvas height.
        var full = SpriteExtents.From(new Vector2(70f, 10f), new Vector2(20f, 20f), Canvas, WorldHeight);
        var half = SpriteExtents.From(new Vector2(70f, 10f), new Vector2(20f, 20f), Canvas, WorldHeight / 2f);

        Assert.Equal(full.Width / 2f, half.Width, 5);
        Assert.Equal(full.Height / 2f, half.Height, 5);
        Assert.Equal(full.CenterXOffset / 2f, half.CenterXOffset, 5);
        Assert.Equal(full.CenterYOffset / 2f, half.CenterYOffset, 5);
    }

    [Fact]
    public void ANonSquareCanvasScalesBothAxesByTheSameFactor()
    {
        // The factor comes off the canvas *height* alone, so a wide canvas produces an extent
        // wider than it is tall rather than squashing it back to square.
        var extent = SpriteExtents.From(Vector2.Zero, new Vector2(100f, 200f), new Vector2(100f, 200f), 2f);

        Assert.Equal(1f, extent.Width, 5);
        Assert.Equal(2f, extent.Height, 5);
    }
}
