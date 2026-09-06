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
        // Sight radius (15m) is several cells wide (5m each) - a single source should reveal a
        // small neighborhood around it, not just the one cell it happens to stand in.
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
        // A position a hair west of the origin belongs to the cell west of it, not to the
        // origin's own - truncation would fold the whole band from -2.5 to 2.5 into one cell
        // twice the width of every other.
        Assert.Equal(new ExplorationCell(cellX, cellY), ExplorationState.CellFor(new Position(x, y)));
    }

    [Fact]
    public void OneSourceSeesAFixedNumberOfCells()
    {
        // A 15m sight radius over 2.5m cells, counted by cell centre - exact rather than
        // "more than one", because the ring bounds and the centre offset are what decide
        // whether sight reads as a circle or as a coarse diamond.
        var exploration = new ExplorationState();

        exploration.Update([new Position(0, 0)]);

        Assert.Equal(112, exploration.Explored.Count);
    }

    [Fact]
    public void TheOutermostRingCountsOnlyWhenTheSourceStandsOffCentreInItsCell()
    {
        // Six cells out is the furthest the loop reaches. From the origin that ring's centres
        // fall outside the radius, but a source standing towards the east edge of its own cell
        // reaches the eastern one - so the loop genuinely needs its last step.
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
        // Translating a source by a whole number of cells translates what it sees, unchanged -
        // the radius test measures from the source to each cell centre, so it must not pick up
        // any dependence on absolute position.
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
        // The cell two steps north-east has its centre 17.7m out - beyond the 15m radius -
        // even though its nearest corner is well inside. Measuring from the corner would let
        // sight bulge into a diamond at the diagonals.
        var exploration = new ExplorationState();

        exploration.Update([new Position(0, 0)]);

        Assert.True(exploration.IsVisible(new ExplorationCell(3, 3)));
        Assert.False(exploration.IsVisible(new ExplorationCell(5, 5)));
    }

    [Fact]
    public void TheOutermostRingCountsOnBothAxesNotJustEastWest()
    {
        // The north-south sweep has to reach exactly as far as the east-west one, and a cell's
        // centre has to be measured northward the same way it is eastward - a sign or a bound
        // wrong on one axis alone would show as sight reaching further one way than the other.
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
