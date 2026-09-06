using ManyWinters.Core.World;

namespace ManyWinters.Tests.World;

public class PositionTests
{
    private static readonly Position Target = new(10, 10);

    [Theory]
    [InlineData(30, 10)]
    [InlineData(30, 25)]
    [InlineData(-14, -8)]
    [InlineData(10, 30)]
    [InlineData(10, -22)]
    public void ApproachStopsExactlyTheStandoffShortOfTheTarget(double fromX, double fromY)
    {
        var approach = Position.Approach(new Position(fromX, fromY), Target, 1.2);

        Assert.Equal(1.2, WorldState.Distance(approach, Target), precision: 9);
    }

    [Theory]
    [InlineData(30, 10)]
    [InlineData(30, 25)]
    [InlineData(-14, -8)]
    [InlineData(10, 30)]
    [InlineData(10, -22)]
    public void ApproachLiesOnTheStraightLineFromTheStartToTheTarget(double fromX, double fromY)
    {
        var from = new Position(fromX, fromY);

        var approach = Position.Approach(from, Target, 1.2);

        // Cross product of "from -> target" with "from -> approach": zero on the line.
        var cross = ((Target.X - from.X) * (approach.Y - from.Y)) - ((Target.Y - from.Y) * (approach.X - from.X));
        Assert.Equal(0, cross, precision: 9);
    }

    [Fact]
    public void ApproachEndsOnTheSideItSetOffFrom()
    {
        // Two walkers converging from opposite sides each stop on their own side of the
        // target, not both at one agreed spot.
        var fromWest = Position.Approach(new Position(-20, 10), Target, 1.2);
        var fromEast = Position.Approach(new Position(40, 10), Target, 1.2);

        AssertPosition(8.8, 10, fromWest);
        AssertPosition(11.2, 10, fromEast);
    }

    [Fact]
    public void ApproachStaysPutWhenAlreadyInsideTheStandoff()
    {
        var from = new Position(10.5, 10);

        Assert.Equal(from, Position.Approach(from, Target, 1.2));
    }

    [Fact]
    public void ApproachStaysPutWhenExactlyAtTheStandoff()
    {
        // Exactly the standoff away is already the destination - walking there would be a
        // zero-length move, so it's the start itself, not a recomputed copy of it.
        var from = new Position(Target.X + 1.2, Target.Y);

        Assert.Equal(from, Position.Approach(from, Target, 1.2));
    }

    [Fact]
    public void ApproachStaysPutWhenStandingOnTheTarget()
    {
        // Distance zero has no direction to back off along - rather than dividing by it, the
        // walker simply stays where they are.
        var approach = Position.Approach(Target, Target, 1.2);

        Assert.Equal(Target, approach);
    }

    [Fact]
    public void ApproachScalesWithTheStandoffItIsGiven()
    {
        var from = new Position(20, 10);

        AssertPosition(13, 10, Position.Approach(from, Target, 3));
        AssertPosition(10.5, 10, Position.Approach(from, Target, 0.5));
    }

    // Computed coordinates, so compared to a tolerance rather than bit-for-bit.
    private static void AssertPosition(double expectedX, double expectedY, Position actual)
    {
        Assert.Equal(expectedX, actual.X, precision: 9);
        Assert.Equal(expectedY, actual.Y, precision: 9);
    }
}
