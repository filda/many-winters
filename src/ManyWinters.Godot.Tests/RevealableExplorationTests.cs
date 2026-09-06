using ManyWinters.Core.World;

namespace ManyWinters.Godot.Tests;

public class RevealableExplorationTests
{
    // Well outside SightRadiusMeters of the origin, where the one sight source below stands.
    private static readonly ExplorationCell FarCell = new(100, 100);
    private static readonly ExplorationCell HomeCell = ExplorationState.CellFor(new Position(0, 0));

    private static ExplorationState ExploredAroundTheOrigin()
    {
        var exploration = new ExplorationState();
        exploration.Update([new Position(0, 0)]);
        return exploration;
    }

    [Fact]
    public void WithoutTheRevealANeverExploredCellStaysUnknown()
    {
        var lens = new RevealableExploration(ExploredAroundTheOrigin());

        Assert.False(lens.IsExplored(FarCell));
        Assert.False(lens.IsVisible(FarCell));
    }

    [Fact]
    public void WithoutTheRevealTheRealStatePassesThrough()
    {
        var lens = new RevealableExploration(ExploredAroundTheOrigin());

        Assert.True(lens.IsExplored(HomeCell));
        Assert.True(lens.IsVisible(HomeCell));
    }

    [Fact]
    public void RevealingMakesEveryCellExploredAndInSight()
    {
        var lens = new RevealableExploration(new ExplorationState()) { RevealAll = true };

        Assert.True(lens.IsExplored(FarCell));
        Assert.True(lens.IsVisible(FarCell));
    }

    [Fact]
    public void SwitchingTheRevealOffRestoresTheRealState()
    {
        // The reveal must not leak into ExplorationState itself - that would mark the whole
        // map as visited for good, and the fog could never come back.
        var lens = new RevealableExploration(ExploredAroundTheOrigin()) { RevealAll = true };

        lens.RevealAll = false;

        Assert.False(lens.IsExplored(FarCell));
        Assert.False(lens.IsVisible(FarCell));
        Assert.True(lens.IsVisible(HomeCell));
    }
}
