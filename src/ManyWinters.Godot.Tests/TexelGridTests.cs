using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// A 100m-wide map in 40 texels: 2.5m each, and no round number that would let a wrong
// operation give the right answer by accident.
public class TexelGridTests
{
    private static readonly TexelGrid Grid = new(Size: 40, HalfExtentMeters: 50f);

    [Fact]
    public void OneTexelStandsForItsShareOfTheMap()
    {
        Assert.Equal(2.5f, Grid.MetresPerTexel, 5);
        Assert.Equal(100f, Grid.ExtentMeters, 5);
    }

    [Fact]
    public void ATexelMapsToItsOwnCentreNotItsCorner()
    {
        // Texel 0 spans -50 to -47.5, so its centre is -48.75. Its corner would bias every
        // sample half a texel toward the map's origin.
        Assert.Equal(-48.75f, Grid.WorldAt(0), 5);
        Assert.Equal(48.75f, Grid.WorldAt(39), 5);
    }

    [Fact]
    public void TheMiddleTexelSitsJustPastTheOriginBecauseAnEvenGridHasNoCentreTexel()
    {
        // 40 texels means the origin falls on the seam between 19 and 20, so neither is
        // centred on it - worth pinning, because "texel 20 is at 0" is the natural guess.
        Assert.Equal(1.25f, Grid.WorldAt(20), 5);
        Assert.Equal(-1.25f, Grid.WorldAt(19), 5);
    }

    [Fact]
    public void GoingOutToTheWorldAndBackReturnsTheSameTexel()
    {
        // The whole reason both directions live together: they are exact inverses, even though
        // one adds half a texel and the other does not.
        for (var texel = 0; texel < Grid.Size; texel++)
        {
            Assert.Equal(texel, Grid.TexelAt(Grid.WorldAt(texel)));
        }
    }

    [Fact]
    public void AWorldPointLandsInTheTexelThatContainsIt()
    {
        // Just inside each side of texel 20 (which spans 0 to 2.5) and just outside it.
        Assert.Equal(20, Grid.TexelAt(0.01f));
        Assert.Equal(20, Grid.TexelAt(2.49f));
        Assert.Equal(19, Grid.TexelAt(-0.01f));
        Assert.Equal(21, Grid.TexelAt(2.51f));
    }

    [Fact]
    public void APointOffTheMapClampsToTheNearestEdgeTexel()
    {
        // Callers legitimately ask about spots slightly outside - a cloud candidate near the
        // edge - and wrapping would answer with the far side of the map.
        Assert.Equal(0, Grid.TexelAt(-1000f));
        Assert.Equal(39, Grid.TexelAt(1000f));
        Assert.Equal(0, Grid.TexelAt(-50f));
        Assert.Equal(39, Grid.TexelAt(50f));
    }

    [Fact]
    public void CoveringRoundsUpSoTheBitmapNeverStopsShortOfTheMapEdge()
    {
        // 100m of map in 7m cells is 14.28 cells: 15 texels, not 14, or the far edge would
        // have no texel describing it at all.
        var grid = TexelGrid.Covering(halfExtentMeters: 50f, cellSizeMeters: 7f);

        Assert.Equal(15, grid.Size);
        Assert.True(grid.MetresPerTexel <= 7f, "a rounded-up count means texels no larger than a cell");
    }

    [Fact]
    public void CoveringUsesTheCellSizeExactlyWhenItDivides()
    {
        Assert.Equal(40, TexelGrid.Covering(50f, 2.5f).Size);
    }

    [Fact]
    public void AGridOfADifferentSizeScalesEverythingWithIt()
    {
        // Guards the extent and the size against each other: halving the texel count doubles
        // what one texel covers, and the map is still the same width.
        var coarse = new TexelGrid(20, 50f);

        Assert.Equal(5f, coarse.MetresPerTexel, 5);
        Assert.Equal(-47.5f, coarse.WorldAt(0), 5);
        Assert.Equal(100f, coarse.ExtentMeters, 5);
    }
}
