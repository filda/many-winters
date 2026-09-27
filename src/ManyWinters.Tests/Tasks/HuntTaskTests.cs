using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Tasks;

// HuntTask only ever walks (docs/todo/fauna-plan.md, phase 3) - the throw itself is
// WorldState.Advance's call, gated on range and NextAttemptTick, tested at that level
// (WorldStateHuntingIdleTests). Closes in on *moving* prey with a fresh MoveTask every tick, the
// same pattern FollowTask uses for a target that does not sit still.
public class HuntTaskTests
{
    private const float Range = 10f;

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
        var task = new HuntTask(prey, Range);
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

        var task = new HuntTask(prey, Range);

        Assert.Same(prey, task.Prey);
        Assert.Equal(Range, task.Range);
    }

    [Fact]
    public void StartsWithNoAttemptMadeYet()
    {
        var task = new HuntTask(NewPrey(new Position(30, 10)), Range);

        Assert.Equal(0, task.NextAttemptTick);
    }

    [Fact]
    public void StandsStillOnceAlreadyWithinRange()
    {
        var prey = NewPrey(new Position(5, 0));
        var task = new HuntTask(prey, Range);
        var hunter = NewHunter(new Position(0, 0));

        task.Advance(hunter);

        Assert.Equal(new Position(0, 0), hunter.Position);
    }

    [Fact]
    public void ClosesInOnPreyThatIsOutOfRange()
    {
        var prey = NewPrey(new Position(30, 0));
        var task = new HuntTask(prey, Range);
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
    // GatherTask's own resource that never moves.
    [Fact]
    public void FollowsPreyThatIsStillMoving()
    {
        var prey = NewPrey(new Position(0, 0));
        var task = new HuntTask(prey, Range);
        var hunter = NewHunter(new Position(0, 0));

        // Slower than HuntTask's own walking speed (0.3/tick), the same "the target itself
        // moves on, more slowly than the follower" case FollowTaskTests covers - a target that
        // outran the hunter's approach speed would never be caught, which is a fact about the
        // chase and not what this test is about.
        for (var i = 0; i < 400; i++)
        {
            prey.Position = new Position(prey.Position.X + 0.1, 0);
            task.Advance(hunter);
        }

        Assert.True(WorldState.Distance(hunter.Position, prey.Position) <= Range);
    }
}
