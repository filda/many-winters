using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

// Phase 2's "útěk" milestone (docs/todo/fauna-plan.md, "Útěk dřív než lov"): a species with
// SpeciesDefinition.Flee (deer) breaks off whatever it is doing the moment a living person comes
// within FleeDistance, and a species with none (a person) never does.
public class AnimalFleeTests
{
    private static HomeRange NewHome(Position anchor, float radius = 20f) => new(anchor) { Radius = radius, DriftMetresPerSeason = 0f };

    [Fact]
    public void AGrazingDeerDropsItsGatherTaskWhenAPersonWalksWithinFleeDistance()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        world.SpawnResourceNode(TestCatalogs.Grass, position, amount: 200f);
        var deer = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, position, NewHome(position));
        deer.Needs.Hunger = 60f;
        world.Advance(1);
        Assert.IsType<GatherTask>(deer.Tasks.Current);

        world.SpawnPerson("Ava", new Position(TestCatalogs.DeerFleeDistance - 1, 0));
        world.Advance(1);

        var flee = Assert.IsType<FleeTask>(deer.Tasks.Current);
        Assert.Equal("Ava", ((Person)flee.Threat).Name);
    }

    [Fact]
    public void ADeerResumesGrazingAfterThePersonLeaves()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        // Near-endless, like DeerHerdMilestoneTests.ScatterGrass - a small node would be stripped
        // to zero by the deer's very first harvest and only slowly regrow (GatherCommand's harvest
        // is 20-40 units a tick against a 200-unit node), so sampling a fixed number of ticks later
        // could land on a drained-but-not-yet-regrown moment that has nothing to do with fleeing.
        world.SpawnResourceNode(TestCatalogs.Grass, position, amount: 1_000_000f);
        var deer = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, position, NewHome(position));
        deer.Needs.Hunger = 60f;
        var person = world.SpawnPerson("Ava", new Position(TestCatalogs.DeerFleeDistance - 1, 0));
        world.Advance(1);
        Assert.IsType<FleeTask>(deer.Tasks.Current);

        // Moved well beyond SafeDistance from wherever the deer now stands - the flee completes
        // (and the empty queue that follows reconsiders) within the very next tick.
        person.Position = new Position(1000, 1000);
        world.Advance(1);

        Assert.IsType<GatherTask>(deer.Tasks.Current);
    }

    [Fact]
    public void ADeerBeyondFleeDistanceIgnoresANearbyPerson()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        var deer = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, position, NewHome(position));
        world.SpawnPerson("Ava", new Position(TestCatalogs.DeerFleeDistance + 5, 0));

        world.Advance(1);

        Assert.IsType<IdleTask>(deer.Tasks.Current);
    }

    [Fact]
    public void APersonNeverFleesAnotherPersonStandingRightNextToThem()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        var person = world.SpawnPerson("Ava", position, TestCatalogs.AdultAgeTicks);
        world.SpawnPerson("Bran", new Position(0.5, 0), TestCatalogs.AdultAgeTicks);

        world.Advance(5);

        Assert.IsNotType<FleeTask>(person.Tasks.Current);
    }

    // The same threat does not restart the task every tick (WorldState.KeepsCurrentTask) - proven
    // by reference identity of the FleeTask instance across several ticks while the threat stays
    // in range the whole time.
    [Fact]
    public void TheSameThreatDoesNotRestartTheFleeTaskEveryTick()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        var deer = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, position, NewHome(position, radius: 100f));
        world.SpawnPerson("Ava", new Position(TestCatalogs.DeerFleeDistance - 2, 0));

        world.Advance(1);
        var firstTask = Assert.IsType<FleeTask>(deer.Tasks.Current);

        world.Advance(1);
        var secondTask = Assert.IsType<FleeTask>(deer.Tasks.Current);

        Assert.Same(firstTask, secondTask);
    }

    [Fact]
    public void ACalfResumesNursingAfterFleeingAndReturningToItsMother()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        // A small home radius keeps the mother's own idle wander close to the anchor while the
        // calf runs off well past it, so the calf's own long walk back is not chasing a mother
        // who may have wandered off just as far in some other direction.
        var home = NewHome(new Position(0, 0), radius: 2f);
        var mother = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(0, 0), home, TestCatalogs.AdultAgeTicks, Sex.Female);
        // At the mother's own nursing reach (2, exactly SimulationRules.MaxInteractionDistance).
        var calf = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(2, 0), home, initialAgeTicks: 0, sex: Sex.Male, mother: mother);
        Assert.True(world.IsBeingNursed(calf));

        // Close enough to the calf to send it fleeing (distance 7 < FleeDistance 8) but far enough
        // from the mother that she does not (distance 9, not < 8), so only the calf runs and the
        // two separate.
        world.SpawnPerson("Ava", new Position(2 + TestCatalogs.DeerFleeDistance - 1, 0));
        world.Advance(1);
        Assert.IsType<FleeTask>(calf.Tasks.Current);

        var wasEverUnnursed = false;
        for (var i = 0; i < 100; i++)
        {
            world.Advance(1);
            wasEverUnnursed |= !world.IsBeingNursed(calf);
        }

        Assert.True(wasEverUnnursed, "expected the calf to lose nursing reach while it fled");
        Assert.True(world.IsBeingNursed(calf), "expected the calf to be nursed again once it walked back to its mother");
    }
}
