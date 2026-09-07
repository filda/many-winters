using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

public class ExplorationMasksTests
{
    // Small enough to reason about cell by cell: 8 texels across a 20m map, so one texel per
    // 2.5m exploration cell.
    private static readonly TexelGrid Grid = new(Size: 8, HalfExtentMeters: 10f);

    private static RevealableExploration WithSightAt(params Position[] sources)
    {
        var state = new ExplorationState();
        state.Update(sources);

        return new RevealableExploration(state);
    }

    [Fact]
    public void GroundNobodyHasSeenIsUnknownAndNothingElse()
    {
        var masks = ExplorationMasks.Build(WithSightAt(), Grid);

        for (var ty = 0; ty < Grid.Size; ty++)
        {
            for (var tx = 0; tx < Grid.Size; tx++)
            {
                Assert.Equal(1f, masks.Unexplored[ty, tx]);
                Assert.Equal(0f, masks.Remembered[ty, tx]);
                Assert.False(masks.Explored[ty, tx]);
            }
        }
    }

    [Fact]
    public void GroundSomeoneIsStandingOnIsNeitherUnknownNorRemembered()
    {
        // The two tiers are exclusive: what is in sight right now is plain visible, so both
        // masks read zero there even though it is explored.
        var masks = ExplorationMasks.Build(WithSightAt(new Position(0, 0)), Grid);
        var centre = Grid.TexelAt(0f);

        Assert.Equal(0f, masks.Unexplored[centre, centre]);
        Assert.Equal(0f, masks.Remembered[centre, centre]);
        Assert.True(masks.Explored[centre, centre]);
    }

    [Fact]
    public void GroundSeenBeforeButOutOfSightNowIsRemembered()
    {
        var state = new ExplorationState();
        state.Update([new Position(0, 0)]);
        state.Update([new Position(500, 500)]);
        var masks = ExplorationMasks.Build(new RevealableExploration(state), Grid);
        var centre = Grid.TexelAt(0f);

        Assert.Equal(0f, masks.Unexplored[centre, centre]);
        Assert.Equal(1f, masks.Remembered[centre, centre]);
        Assert.True(masks.Explored[centre, centre]);
    }

    [Fact]
    public void RevealingTheMapMakesEverythingReadAsSeenAndInSight()
    {
        // The inspector's debugging toggle: the whole map counts as explored *and* visible, so
        // neither fog tier shows anywhere.
        var exploration = WithSightAt();
        exploration.RevealAll = true;

        var masks = ExplorationMasks.Build(exploration, Grid);

        for (var ty = 0; ty < Grid.Size; ty++)
        {
            for (var tx = 0; tx < Grid.Size; tx++)
            {
                Assert.Equal(0f, masks.Unexplored[ty, tx]);
                Assert.Equal(0f, masks.Remembered[ty, tx]);
                Assert.True(masks.Explored[ty, tx]);
            }
        }
    }

    [Fact]
    public void TheExploredFlagIsTheExactComplementOfTheUnknownMask()
    {
        // The distance field measures out from this flag, so a boundary that disagreed with
        // the mask the shader gates on would fade from the wrong edge.
        var masks = ExplorationMasks.Build(WithSightAt(new Position(0, 0)), Grid);

        for (var ty = 0; ty < Grid.Size; ty++)
        {
            for (var tx = 0; tx < Grid.Size; tx++)
            {
                Assert.Equal(masks.Explored[ty, tx], masks.Unexplored[ty, tx] == 0f);
            }
        }
    }

    [Fact]
    public void TheMasksAreIndexedRowThenColumnSoAnOffCentreSightSourceLandsWhereItShould()
    {
        // Deliberately asymmetric: sight far along one axis only. Swapping the two indices
        // anywhere in the build would mirror the fog across the diagonal, which a square map
        // with a centred source would never reveal.
        var masks = ExplorationMasks.Build(WithSightAt(new Position(8f, -8f)), Grid);
        var tx = Grid.TexelAt(8f);
        var ty = Grid.TexelAt(-8f);

        Assert.True(masks.Explored[ty, tx], "the source's own texel should be explored");
        Assert.False(masks.Explored[tx, ty], "and its mirror across the diagonal should not");
    }

    [Fact]
    public void EveryMaskCoversTheWholeGrid()
    {
        var masks = ExplorationMasks.Build(WithSightAt(), Grid);

        Assert.Equal(Grid.Size, masks.Unexplored.GetLength(0));
        Assert.Equal(Grid.Size, masks.Unexplored.GetLength(1));
        Assert.Equal(Grid.Size, masks.Remembered.GetLength(0));
        Assert.Equal(Grid.Size, masks.Explored.GetLength(0));
    }
}
