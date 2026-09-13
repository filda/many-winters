using ManyWinters.Core.World;

namespace ManyWinters.Tests.World;

public class ExplorationStateTests
{
    [Fact]
    public void NothingIsExploredOrVisibleBeforeAnyUpdate()
    {
        var exploration = new ExplorationState();

        var cell = ExplorationState.CellFor(new Position(0, 0));

        Assert.False(exploration.IsExplored(cell));
        Assert.False(exploration.IsVisible(cell));
    }

    [Fact]
    public void UpdateMarksTheSourcesOwnCellAsVisibleAndExplored()
    {
        var exploration = new ExplorationState();

        exploration.Update([new Position(0, 0)]);

        var cell = ExplorationState.CellFor(new Position(0, 0));
        Assert.True(exploration.IsVisible(cell));
        Assert.True(exploration.IsExplored(cell));
    }

    [Fact]
    public void UpdateDoesNotMarkCellsFarBeyondSightRadius()
    {
        var exploration = new ExplorationState();

        exploration.Update([new Position(0, 0)]);

        var farCell = ExplorationState.CellFor(new Position(ExplorationState.SightRadiusMeters * 10, 0));
        Assert.False(exploration.IsVisible(farCell));
        Assert.False(exploration.IsExplored(farCell));
    }

    [Fact]
    public void ExploredCellsStayExploredAfterTheSourceMovesAway()
    {
        var exploration = new ExplorationState();
        var originCell = ExplorationState.CellFor(new Position(0, 0));

        exploration.Update([new Position(0, 0)]);
        exploration.Update([new Position(ExplorationState.SightRadiusMeters * 10, 0)]);

        Assert.True(exploration.IsExplored(originCell));
        Assert.False(exploration.IsVisible(originCell));
    }

    [Fact]
    public void VisibleReflectsOnlyTheMostRecentUpdate()
    {
        var exploration = new ExplorationState();
        var originCell = ExplorationState.CellFor(new Position(0, 0));
        var farPosition = new Position(ExplorationState.SightRadiusMeters * 10, 0);

        exploration.Update([new Position(0, 0)]);
        exploration.Update([farPosition]);

        Assert.True(exploration.IsVisible(ExplorationState.CellFor(farPosition)));
        Assert.False(exploration.IsVisible(originCell));
    }

    [Fact]
    public void ASourceExploresMoreThanJustItsOwnCell()
    {
        // A 15m sight radius spans several 2.5m cells, so one source reveals a neighbourhood,
        // not just its own cell.
        var exploration = new ExplorationState();

        exploration.Update([new Position(0, 0)]);

        Assert.True(exploration.Explored.Count > 1);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(-0.1, -0.1, -1, -1)]
    [InlineData(6.2, -7.6, 2, -4)]
    [InlineData(2.5, 2.5, 1, 1)]
    public void CellForFloorsTowardNegativeInfinityRatherThanTowardZero(double x, double y, int cellX, int cellY)
    {
        // Truncation would fold -2.5..2.5 into one cell twice the width of every other.
        Assert.Equal(new ExplorationCell(cellX, cellY), ExplorationState.CellFor(new Position(x, y)));
    }

    [Fact]
    public void OneSourceSeesAFixedNumberOfCells()
    {
        // Exact: the ring bounds and the centre offset decide whether sight reads as a circle
        // or as a coarse diamond.
        var exploration = new ExplorationState();

        exploration.Update([new Position(0, 0)]);

        Assert.Equal(112, exploration.Explored.Count);
    }

    [Fact]
    public void TheOutermostRingCountsOnlyWhenTheSourceStandsOffCentreInItsCell()
    {
        // Six cells out is the loop's last step: from the origin that ring's centres fall
        // outside the radius, but a source near its cell's east edge reaches the eastern one.
        var atOrigin = new ExplorationState();
        var offCentre = new ExplorationState();

        atOrigin.Update([new Position(0, 0)]);
        offCentre.Update([new Position(2.4, 0)]);

        Assert.False(atOrigin.IsVisible(new ExplorationCell(6, 0)));
        Assert.True(offCentre.IsVisible(new ExplorationCell(6, 0)));
    }

    [Fact]
    public void SightHasTheSameShapeWhereverTheSourceStands()
    {
        // Translating a source by whole cells translates what it sees; the radius test must not
        // depend on absolute position.
        var near = new ExplorationState();
        var far = new ExplorationState();
        var offset = new ExplorationCell(100, -150);

        near.Update([new Position(0, 0)]);
        far.Update([new Position(offset.X * ExplorationState.CellSizeMeters, offset.Y * ExplorationState.CellSizeMeters)]);

        var translated = near.Explored.Select(c => new ExplorationCell(c.X + offset.X, c.Y + offset.Y)).ToHashSet();
        Assert.Equal(translated, far.Explored.ToHashSet());
    }

    [Fact]
    public void ACellCountsAsSeenByItsOwnCentreNotByItsNearestCorner()
    {
        // Measured by cell centre: (3, 3) is 12.4m out and seen, (5, 5) is 19.4m out and not.
        // Measuring from the nearest corner would bulge sight into a diamond at the diagonals.
        var exploration = new ExplorationState();

        exploration.Update([new Position(0, 0)]);

        Assert.True(exploration.IsVisible(new ExplorationCell(3, 3)));
        Assert.False(exploration.IsVisible(new ExplorationCell(5, 5)));
    }

    [Fact]
    public void TheOutermostRingCountsOnBothAxesNotJustEastWest()
    {
        // The north-south sweep must reach exactly as far as the east-west one; a sign or bound
        // wrong on one axis would show as lopsided sight.
        var atOrigin = new ExplorationState();
        var offCentre = new ExplorationState();

        atOrigin.Update([new Position(0, 0)]);
        offCentre.Update([new Position(0, 2.4)]);

        Assert.False(atOrigin.IsVisible(new ExplorationCell(0, 6)));
        Assert.True(offCentre.IsVisible(new ExplorationCell(0, 6)));
    }

    [Fact]
    public void SightReachesTheSameDistanceInEveryDirection()
    {
        var exploration = new ExplorationState();

        exploration.Update([new Position(0, 0)]);

        var explored = exploration.Explored.ToHashSet();
        foreach (var cell in explored)
        {
            Assert.Contains(new ExplorationCell(-1 - cell.X, cell.Y), explored);
            Assert.Contains(new ExplorationCell(cell.X, -1 - cell.Y), explored);
        }
    }
}
