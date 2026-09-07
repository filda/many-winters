using Godot;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Sprites;

namespace ManyWinters.Godot.Tests;

public class SpriteGeometryTests
{
    private static SpriteVisibleExtent.Extent Extent(float width, float height, float centerX, float centerY) =>
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
}
