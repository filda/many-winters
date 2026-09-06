using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.Tasks;

public class GatherTaskTests
{
    private static readonly Position Target = new(10, 10);

    // The shipped reach - what DecideIdleTask hands in from SimulationRules.
    private static readonly float Reach = SimulationRules.Default.MaxInteractionDistance;

    private static Person NewPerson(Position position) =>
        new() { Id = new PersonId(1), Name = "Ava", BirthTick = 0, Position = position };

    private static ResourceNode NewTargetNode() =>
        new() { Id = new ResourceNodeId(1), Kind = new ResourceKindId("apple"), Position = Target };

    private static GatherTask NewTask(float? reach = null, ResourceNode? target = null) => new(target ?? NewTargetNode(), reach ?? Reach);

    [Fact]
    public void IsNeverComplete()
    {
        // WorldState.Advance re-evaluates every tick whether this is still worth doing, the
        // same way it does for IdleTask - the task itself never declares itself finished.
        var task = NewTask();
        var person = NewPerson(new Position(30, 10));

        for (var i = 0; i < 200; i++)
        {
            task.Advance(person);
            Assert.False(task.IsComplete);
        }
    }

    [Fact]
    public void RemembersWhatItWasSentTo()
    {
        var node = NewTargetNode();

        var task = NewTask(target: node);

        Assert.Same(node, task.Target);
        Assert.Equal(Reach, task.ReachDistance);
    }

    [Fact]
    public void WalksIntoInteractionRangeOfItsTarget()
    {
        var person = NewPerson(new Position(30, 10));
        var task = NewTask();

        for (var i = 0; i < 200; i++)
        {
            task.Advance(person);
        }

        Assert.True(
            WorldState.Distance(person.Position, Target) <= Reach,
            $"Ended up {WorldState.Distance(person.Position, Target)} away, out of gathering reach.");
    }

    [Fact]
    public void StopsAsSoonAsItIsInReachRatherThanStandingOnTheResource()
    {
        var person = NewPerson(new Position(30, 10));
        var task = NewTask();

        for (var i = 0; i < 200; i++)
        {
            task.Advance(person);
        }

        // A person who walks all the way onto the sprite overlaps it visually; gathering only
        // ever needed them to be within reach, so the walk ends the moment they are.
        Assert.True(
            WorldState.Distance(person.Position, Target) > Reach - 0.5,
            $"Walked closer than needed - ended up {WorldState.Distance(person.Position, Target)} away.");
    }

    [Fact]
    public void AShorterReachStillEndsTheWalkInsideIt()
    {
        // The standoff scales with the reach it was handed rather than being a fixed 1.2 - a
        // world whose people have to get closer to gather doesn't leave them stranded at a
        // standoff point they can't gather from.
        const float shortReach = 0.5f;
        var person = NewPerson(new Position(30, 10));
        var task = NewTask(shortReach);

        for (var i = 0; i < 200; i++)
        {
            task.Advance(person);
        }

        var distance = WorldState.Distance(person.Position, Target);
        Assert.True(distance <= shortReach, $"Ended up {distance} away, outside the short reach.");
        Assert.True(distance > 0.1, $"Walked all the way onto the resource ({distance} away).");
    }

    [Fact]
    public void StaysPutWhenItStartsWithinReach()
    {
        var start = new Position(11.5, 10);
        var person = NewPerson(start);
        var task = NewTask();

        for (var i = 0; i < 20; i++)
        {
            task.Advance(person);
        }

        Assert.Equal(start, person.Position);
    }

    [Fact]
    public void StaysPutWhenItStartsExactlyAtTheEdgeOfReach()
    {
        // Exactly the reach distance away is within reach, not one step short of it -
        // gathering works from here, so there's nothing left to walk.
        var start = new Position(Target.X + Reach, Target.Y);
        var person = NewPerson(start);
        var task = NewTask();

        for (var i = 0; i < 20; i++)
        {
            task.Advance(person);
        }

        Assert.Equal(start, person.Position);
    }

    [Theory]
    [InlineData(30, 10)]
    [InlineData(30, 25)]
    [InlineData(-14, -8)]
    [InlineData(10, 30)]
    [InlineData(10, -22)]
    public void ApproachesTheResourceInAStraightLine(double startX, double startY)
    {
        // The standoff point is on the line between where the person set off and the resource,
        // so the whole approach is one straight walk - not a curve out to one side and back.
        var start = new Position(startX, startY);
        var person = NewPerson(start);
        var task = NewTask();

        for (var i = 0; i < 200; i++)
        {
            task.Advance(person);

            // Cross product of "start -> target" with "start -> here": zero while the person
            // stays on that line, and it scales with the distances involved, hence the
            // proportional tolerance rather than a flat one.
            var cross = ((Target.X - start.X) * (person.Position.Y - start.Y))
                - ((Target.Y - start.Y) * (person.Position.X - start.X));
            Assert.True(
                Math.Abs(cross) < 1e-6 * WorldState.Distance(start, Target),
                $"Strayed off the direct line to the resource on tick {i} (cross = {cross}).");
        }
    }

    [Fact]
    public void ApproachesFromWhicheverSideItSetOffFrom()
    {
        // Two people converging on the same resource from opposite sides each stop on their own
        // side of it, rather than both walking around to one agreed spot.
        var west = NewPerson(new Position(-20, 10));
        var east = NewPerson(new Position(40, 10));
        var westTask = NewTask();
        var eastTask = NewTask();

        for (var i = 0; i < 300; i++)
        {
            westTask.Advance(west);
            eastTask.Advance(east);
        }

        Assert.True(west.Position.X < Target.X, $"The western walker ended up east of the resource at {west.Position.X}.");
        Assert.True(east.Position.X > Target.X, $"The eastern walker ended up west of the resource at {east.Position.X}.");
    }
}
