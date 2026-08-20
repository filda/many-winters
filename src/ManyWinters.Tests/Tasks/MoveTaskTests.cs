using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.Tasks;

public class MoveTaskTests
{
    private static Person NewPerson(Position position) =>
        new() { Id = new PersonId(1), Name = "Ava", BirthTick = 0, Position = position };

    [Fact]
    public void DestinationExposesTheGivenDestination()
    {
        var task = new MoveTask(new Position(5, 6), speedPerTick: 1f);

        Assert.Equal(new Position(5, 6), task.Destination);
    }

    [Fact]
    public void IsNotCompleteBeforeReachingTheDestination()
    {
        var person = NewPerson(new Position(0, 0));
        var task = new MoveTask(new Position(10, 0), speedPerTick: 1f);

        task.Advance(person);

        Assert.False(task.IsComplete);
    }

    [Fact]
    public void AdvanceMovesThePersonTowardTheDestinationBySpeedPerTick()
    {
        var person = NewPerson(new Position(0, 0));
        var task = new MoveTask(new Position(10, 0), speedPerTick: 2f);

        task.Advance(person);

        Assert.Equal(new Position(2, 0), person.Position);
    }

    [Fact]
    public void AdvanceMovesProportionallyAlongBothAxesForDiagonalMovement()
    {
        var person = NewPerson(new Position(0, 0));
        var task = new MoveTask(new Position(3, 4), speedPerTick: 2.5f);

        task.Advance(person);

        Assert.Equal(new Position(1.5f, 2f), person.Position);
    }

    [Fact]
    public void ArrivesExactlyWhenRemainingDistanceEqualsSpeed()
    {
        var person = NewPerson(new Position(0, 0));
        var task = new MoveTask(new Position(2, 0), speedPerTick: 2f);

        task.Advance(person);

        Assert.Equal(new Position(2, 0), person.Position);
        Assert.True(task.IsComplete);
    }

    [Fact]
    public void ArrivesWithoutOvershootingWhenCloserThanOneStep()
    {
        var person = NewPerson(new Position(0, 0));
        var task = new MoveTask(new Position(1, 0), speedPerTick: 2f);

        task.Advance(person);

        Assert.Equal(new Position(1, 0), person.Position);
        Assert.True(task.IsComplete);
    }

    [Fact]
    public void MultipleAdvanceCallsAccumulateProgressTowardTheDestination()
    {
        var person = NewPerson(new Position(0, 0));
        var task = new MoveTask(new Position(3, 0), speedPerTick: 1f);

        task.Advance(person);
        Assert.Equal(new Position(1, 0), person.Position);
        Assert.False(task.IsComplete);

        task.Advance(person);
        Assert.Equal(new Position(2, 0), person.Position);
        Assert.False(task.IsComplete);

        task.Advance(person);
        Assert.Equal(new Position(3, 0), person.Position);
        Assert.True(task.IsComplete);
    }

    [Fact]
    public void AdvanceAfterArrivalDoesNothingFurther()
    {
        var person = NewPerson(new Position(0, 0));
        var task = new MoveTask(new Position(2, 0), speedPerTick: 2f);
        task.Advance(person);

        task.Advance(person);

        Assert.Equal(new Position(2, 0), person.Position);
        Assert.True(task.IsComplete);
    }

    [Fact]
    public void AdvanceNeverMovesThePersonAgainOnceArrivedEvenIfTheirPositionChangesAfterward()
    {
        var person = NewPerson(new Position(0, 0));
        var task = new MoveTask(new Position(2, 0), speedPerTick: 2f);
        task.Advance(person);
        Assert.True(task.IsComplete);

        person.Position = new Position(0, 0);
        task.Advance(person);

        Assert.Equal(new Position(0, 0), person.Position);
    }

    [Fact]
    public void AdvanceUsesTheCorrectSignWhenComputingVerticalDistanceToTheDestination()
    {
        var person = NewPerson(new Position(0, 5));
        var task = new MoveTask(new Position(0, 1), speedPerTick: 2f);

        task.Advance(person);

        Assert.Equal(new Position(0, 3), person.Position);
    }
}
