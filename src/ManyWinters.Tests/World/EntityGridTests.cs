using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

public class EntityGridTests
{
    [Fact]
    public void FindsWhatIsWithinTheRadiusAndNothingFurther()
    {
        var grid = new EntityGrid(cellSize: 4);
        var near = Pile(1, 1);
        var onTheEdge = Pile(5, 0);
        var far = Pile(5.1, 0);
        grid.Add(near);
        grid.Add(onTheEdge);
        grid.Add(far);

        Assert.Equal([near, onTheEdge], grid.Within(new Position(0, 0), 5));
    }

    [Fact]
    public void ReachesIntoNeighbouringCellsOnBothSidesOfZero()
    {
        // Cells either side of the origin floor to -1 and 0; truncating instead would file
        // both under 0 and still pass a same-cell test.
        var grid = new EntityGrid(cellSize: 4);
        var west = Pile(-3, -3);
        var east = Pile(3, 3);
        grid.Add(west);
        grid.Add(east);

        Assert.Equal([west, east], grid.Within(new Position(0, 0), 5));
        Assert.Equal([west], grid.Within(new Position(-7, -3), 4));
    }

    [Fact]
    public void AnswersInTheOrderTheEntitiesWereAddedWhicheverCellTheyAreIn()
    {
        // A nearest-wins search keeps the first of a tie, so the order has to be the world's,
        // not whichever cell happens to be read first.
        var grid = new EntityGrid(cellSize: 2);
        var first = Pile(9, 9);
        var second = Pile(0, 0);
        var third = Pile(9, 0);
        grid.Add(first);
        grid.Add(second);
        grid.Add(third);

        Assert.Equal([first, second, third], grid.Within(new Position(4.5, 4.5), 10));
    }

    [Fact]
    public void ARemovedEntityIsNoLongerFound()
    {
        var grid = new EntityGrid(cellSize: 4);
        var kept = Pile(1, 1);
        var removed = Pile(1, 2);
        grid.Add(kept);
        grid.Add(removed);

        grid.Remove(removed);

        Assert.Equal([kept], grid.Within(new Position(0, 0), 5));
    }

    [Fact]
    public void RemovingTheLastEntityOfACellAndAddingAgainStillWorks()
    {
        var grid = new EntityGrid(cellSize: 4);
        var gone = Pile(1, 1);
        grid.Add(gone);
        grid.Remove(gone);
        var back = Pile(2, 2);
        grid.Add(back);

        Assert.Equal([back], grid.Within(new Position(0, 0), 5));
    }

    [Fact]
    public void RemovingSomethingNeverAddedChangesNothing()
    {
        var grid = new EntityGrid(cellSize: 4);
        var kept = Pile(1, 1);
        grid.Add(kept);

        grid.Remove(Pile(1, 1));
        grid.Remove(Pile(50, 50));

        Assert.Equal([kept], grid.Within(new Position(0, 0), 5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ACellMustHaveASize(double cellSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EntityGrid(cellSize));
    }

    [Fact]
    public void TheWorldFindsAnEntityItWasGivenAndForgetsOneItRemoved()
    {
        var world = TestCatalogs.CreateWorld();
        var kept = Pile(1, 1);
        var removed = Pile(2, 2);
        world.AddEntity(kept);
        world.AddEntity(removed);

        world.RemoveEntity(removed);

        Assert.Equal([kept], world.EntitiesWithin(new Position(0, 0), 5));
    }

    private static Entity Pile(double x, double y) => new()
    {
        Kind = TestCatalogs.Apple,
        Category = EntityCategory.Pile,
        Position = new Position(x, y),
        StaticAmount = 1,
    };
}
