using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Tasks;

public class FollowTaskTests
{
    private const float KeepWithin = 2f;
    private const float Speed = 0.25f;

    private static Person NewPerson(string name, Position position) =>
        new() { Name = name, BirthTick = 0, Position = position, Mother = Person.Unknown, Father = Person.Unknown, Sex = TestPeople.AnySex };

    [Fact]
    public void IsNeverComplete()
    {
        // Weaning is WorldState.Advance's call, not the task's - it has no idea the child will
        // one day stop needing this.
        var mother = NewPerson("Sela", new Position(0, 0));
        var task = new FollowTask(mother, KeepWithin, Speed);
        var infant = NewPerson("Bran", new Position(0, 0));

        for (var i = 0; i < 200; i++)
        {
            task.Advance(infant);
            Assert.False(task.IsComplete);
        }
    }

    [Fact]
    public void RemembersWhoItIsFollowing()
    {
        var mother = NewPerson("Sela", new Position(0, 0));

        var task = new FollowTask(mother, KeepWithin, Speed);

        Assert.Same(mother, task.Target);
        Assert.Equal(KeepWithin, task.KeepWithin);
    }

    [Fact]
    public void StandsStillWhileAlreadyCloseEnough()
    {
        var mother = NewPerson("Sela", new Position(0, 0));
        var task = new FollowTask(mother, KeepWithin, Speed);
        var infant = NewPerson("Bran", new Position(1, 0));

        task.Advance(infant);

        Assert.Equal(new Position(1, 0), infant.Position);
    }

    [Fact]
    public void ExactlyAtTheGapStillCountsAsCloseEnough()
    {
        var mother = NewPerson("Sela", new Position(0, 0));
        var task = new FollowTask(mother, KeepWithin, Speed);
        var infant = NewPerson("Bran", new Position(KeepWithin, 0));

        task.Advance(infant);

        Assert.Equal(new Position(KeepWithin, 0), infant.Position);
    }

    [Fact]
    public void WalksOneStepTowardTheTargetOnceTheGapOpens()
    {
        var mother = NewPerson("Sela", new Position(0, 0));
        var task = new FollowTask(mother, KeepWithin, Speed);
        var infant = NewPerson("Bran", new Position(10, 0));

        task.Advance(infant);

        Assert.Equal(10 - Speed, infant.Position.X, precision: 5);
        Assert.Equal(0, infant.Position.Y, precision: 5);
    }

    [Fact]
    public void ClosesTheGapAndThenStopsRatherThanStandingOnTopOfTheTarget()
    {
        var mother = NewPerson("Sela", new Position(0, 0));
        var task = new FollowTask(mother, KeepWithin, Speed);
        var infant = NewPerson("Bran", new Position(10, 0));

        for (var i = 0; i < 200; i++)
        {
            task.Advance(infant);
        }

        Assert.True(WorldState.Distance(infant.Position, mother.Position) <= KeepWithin);
        Assert.True(infant.Position.X > 0);
    }

    [Fact]
    public void FollowsTheTargetWhenTheTargetItselfMovesOn()
    {
        // The whole reason this re-aims every tick instead of computing a destination once,
        // the way GatherTask does for a resource that never moves.
        var mother = NewPerson("Sela", new Position(0, 0));
        var task = new FollowTask(mother, KeepWithin, Speed);
        var infant = NewPerson("Bran", new Position(0, 0));

        for (var i = 0; i < 400; i++)
        {
            mother.Position = new Position(mother.Position.X + 0.1, 0);
            task.Advance(infant);
        }

        Assert.True(WorldState.Distance(infant.Position, mother.Position) <= KeepWithin);
    }

    [Fact]
    public void FallsBehindATargetThatWalksFasterThanTheFollower()
    {
        var mother = NewPerson("Sela", new Position(0, 0));
        var task = new FollowTask(mother, KeepWithin, Speed);
        var infant = NewPerson("Bran", new Position(0, 0));

        for (var i = 0; i < 100; i++)
        {
            mother.Position = new Position(mother.Position.X + 1, 0);
            task.Advance(infant);
        }

        Assert.True(WorldState.Distance(infant.Position, mother.Position) > KeepWithin);
    }
}
