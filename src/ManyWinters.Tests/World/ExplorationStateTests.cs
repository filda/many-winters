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
}
