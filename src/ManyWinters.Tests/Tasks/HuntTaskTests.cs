using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Tasks;

// HuntTask only ever walks - the throw itself is WorldState.Advance's call, gated on range and
// NextAttemptTick, tested at that level. Closes in on *moving* prey with a fresh MoveTask every
// tick, the same pattern FollowTask uses for a target that does not sit still.
public class HuntTaskTests
{
    private const float Range = 10f;

    // Most tests here are about the walk itself, not about which speed installed it, so they all
    // share the idle AI's unhurried pace (WorldState.DecideIdleTask) unless the test says
    // otherwise.
    private const float IdleSpeed = GatherTask.SpeedPerTick;

    private static Person NewHunter(Position position) =>
        new() { Name = "Ava", BirthTick = 0, Position = position, Mother = Person.Unknown, Father = Person.Unknown, Sex = TestPeople.AnySex };

    private static Animal NewPrey(Position position) =>
        new(TestCatalogs.DeerSpeciesId, new HomeRange(position) { Radius = 10f, DriftMetresPerSeason = 0f })
        {
            BirthTick = 0,
            Sex = Sex.Female,
            Position = position,
        };

    [Fact]
    public void IsNeverComplete()
    {
        var prey = NewPrey(new Position(30, 10));
        var task = new HuntTask(prey, Range, IdleSpeed);
        var hunter = NewHunter(new Position(0, 0));

        for (var i = 0; i < 200; i++)
        {
            task.Advance(hunter);
            Assert.False(task.IsComplete);
        }
    }

    [Fact]
    public void RemembersWhatItWasSentTo()
    {
        var prey = NewPrey(new Position(30, 10));

        var task = new HuntTask(prey, Range, IdleSpeed);

        Assert.Same(prey, task.Prey);
        Assert.Equal(Range, task.Range);
        Assert.Equal(IdleSpeed, task.SpeedPerTick);
    }

    // The bug this constructor parameter fixes: a player-directed hunt (TargetActions,
    // MoveCommand.SpeedPerTick) has to close the gap faster than the autonomous idle AI's
    // unhurried pace (WorldState.DecideIdleTask, GatherTask.SpeedPerTick) - both used to
    // hard-code the slower one regardless of who sent the hunter.
    [Fact]
    public void ADirectedHuntClosesTheDistanceFasterThanAnIdleOne()
    {
        var directedTask = new HuntTask(NewPrey(new Position(30, 0)), Range, 1f);
        var directedHunter = NewHunter(new Position(0, 0));
        var idleTask = new HuntTask(NewPrey(new Position(30, 0)), Range, IdleSpeed);
        var idleHunter = NewHunter(new Position(0, 0));

        directedTask.Advance(directedHunter);
        idleTask.Advance(idleHunter);

        Assert.True(
            directedHunter.Position.X > idleHunter.Position.X,
            $"Directed hunter reached x={directedHunter.Position.X}, no further than idle hunter's x={idleHunter.Position.X}.");
    }

    [Fact]
    public void StartsWithNoAttemptMadeYet()
    {
        var task = new HuntTask(NewPrey(new Position(30, 10)), Range, IdleSpeed);

        Assert.Equal(0, task.NextAttemptTick);
    }

    [Fact]
    public void StandsStillOnceAlreadyWithinRange()
    {
        var prey = NewPrey(new Position(5, 0));
        var task = new HuntTask(prey, Range, IdleSpeed);
        var hunter = NewHunter(new Position(0, 0));

        task.Advance(hunter);

        Assert.Equal(new Position(0, 0), hunter.Position);
    }

    [Fact]
    public void ClosesInOnPreyThatIsOutOfRange()
    {
        var prey = NewPrey(new Position(30, 0));
        var task = new HuntTask(prey, Range, IdleSpeed);
        var hunter = NewHunter(new Position(0, 0));

        for (var i = 0; i < 200; i++)
        {
            task.Advance(hunter);
        }

        Assert.True(
            WorldState.Distance(hunter.Position, prey.Position) <= Range,
            $"Ended up {WorldState.Distance(hunter.Position, prey.Position)} away, out of hunting range.");
    }

    // The whole reason this re-aims every tick instead of a single computed leg, unlike
    // GatherTask's resource that never moves.
    [Fact]
    public void FollowsPreyThatIsStillMoving()
    {
        var prey = NewPrey(new Position(0, 0));
        var task = new HuntTask(prey, Range, IdleSpeed);
        var hunter = NewHunter(new Position(0, 0));

        // Slower than HuntTask's walking speed (0.3/tick): a target moving more slowly than the
        // follower still eventually gets caught. A target that outran the hunter's approach
        // speed would never be caught, which is a fact about the chase and not what this test
        // is about.
        for (var i = 0; i < 400; i++)
        {
            prey.Position = new Position(prey.Position.X + 0.1, 0);
            task.Advance(hunter);
        }

        Assert.True(WorldState.Distance(hunter.Position, prey.Position) <= Range);
    }
}
