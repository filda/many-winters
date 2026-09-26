using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

// Phase 1a's milestone (docs/todo/fauna-plan.md): a deer herd grazes when hungry, wanders
// otherwise, and starves or dies of old age like anyone else.
public class AnimalSimulationTests
{
    private static HomeRange NewHome(Position anchor, float radius = 20f) => new(anchor) { Radius = radius, DriftMetresPerSeason = 0f };

    [Fact]
    public void AnAnimalWithGrassInRangeEatsWhenHungryAndItsHungerDrops()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        world.SpawnResourceNode(TestCatalogs.Grass, position, amount: 200f);
        var deer = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, position, NewHome(position));
        deer.Needs.Hunger = 60f;

        world.Advance(3);

        Assert.True(deer.Needs.Hunger < 60f, $"Hunger did not drop (stayed at {deer.Needs.Hunger}).");
    }

    [Fact]
    public void AGrazingAnimalNeverPocketsWhatItEats()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        world.SpawnResourceNode(TestCatalogs.Grass, position, amount: 200f);
        var deer = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, position, NewHome(position));
        deer.Needs.Hunger = 60f;

        world.Advance(5);

        Assert.Equal(0, deer.Inventory.Get(TestCatalogs.GrassItem));
        Assert.Empty(deer.Inventory.Counts);
    }

    [Fact]
    public void AnAnimalThatIsNotHungryGetsNoGatherTaskDespiteGrassBeingRightThere()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        world.SpawnResourceNode(TestCatalogs.Grass, position, amount: 200f);
        var deer = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, position, NewHome(position));
        deer.Needs.Hunger = 0f;

        world.Advance(5);

        Assert.IsType<IdleTask>(deer.Tasks.Current);
    }

    [Fact]
    public void AnAnimalWithNoGrassInReachStarvesAtItsOwnMaxHunger()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        var deer = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, position, NewHome(position));

        world.Advance(400);

        Assert.False(deer.IsAlive);
        Assert.Equal(DeathCause.Hunger, deer.CauseOfDeath);
        Assert.True(deer.Needs.Hunger >= deer.MaxHunger);
    }

    [Fact]
    public void AnAnimalDiesOfOldAgeAtItsSpeciesOwnLifespanWhileAPersonBesideItDoesNot()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var rules = world.Configuration.Rules;
        var position = new Position(0, 0);

        // Already at the deer's own MaxLifespanYears (8) - the next tick tips it into old age.
        var deer = world.SpawnAnimal(
            TestCatalogs.DeerSpeciesId,
            position,
            NewHome(position),
            initialAgeTicks: TestCatalogs.DeerLifeCycle.MaxLifespanYears * rules.TicksPerYear);

        // A young adult person, nowhere near the human MaxLifespanYears (10).
        var person = world.SpawnPerson("Ava", position, TestCatalogs.AdultAgeTicks);

        world.Advance(1);

        Assert.False(deer.IsAlive);
        Assert.Equal(DeathCause.OldAge, deer.CauseOfDeath);
        Assert.True(person.IsAlive);
    }

    [Fact]
    public void AnAnimalNeverRevealsExploration()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var farAway = new Position(400, 400);
        world.SpawnAnimal(TestCatalogs.DeerSpeciesId, farAway, NewHome(farAway));

        world.Advance(10);

        Assert.Empty(world.Exploration.Explored);
    }

    [Fact]
    public void AHungryAnimalIsSentToGraze()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        world.SpawnResourceNode(TestCatalogs.Grass, position, amount: 200f);
        var deer = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, position, NewHome(position));
        deer.Needs.Hunger = 60f;

        world.Advance(1);

        // The gather this same tick executes as eating, not pocketing - see
        // AGrazingAnimalNeverPocketsWhatItEats. This one only pins the task itself.
        Assert.IsType<GatherTask>(deer.Tasks.Current);
    }

    // WorldState.FindNearestGatherableEntity's in-home tier only counts a node that would still
    // give this deer a full harvest (GatherCommand.WouldYieldAFullHarvest) - a node down to a
    // sliver still passes IsWorthGathering's plain "more than zero left", so without this a herd
    // would nibble its own barely-regrown home tuft at regen speed forever rather than falling
    // through to fuller grass further out (docs/todo/fauna-plan.md phase 1b, "the shipped map's
    // herds starving").
    [Fact]
    public void AHerdWhoseHomeNodesAreNearlyEmptyGoesToTheFullerGrassOutsideTheHome()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var homeCenter = new Position(0, 0);
        var home = NewHome(homeCenter, radius: 15f);

        var nearlyEmptyInsideHome = world.SpawnResourceNode(TestCatalogs.Grass, new Position(2, 0), amount: 200f);
        nearlyEmptyInsideHome.Growth!.RemainingAmount = 1f;
        var fullOutsideHome = world.SpawnResourceNode(TestCatalogs.Grass, new Position(40, 0), amount: 200f);

        // Standing well out toward the edge of its own territory, close to the outside node -
        // both tiers pick nearest-to-the-deer-itself, not nearest-to-the-anchor, so this is what
        // lets the outside node win over the anchor-hugging depleted one once the home tier has
        // ruled the depleted one out.
        var deer = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(35, 0), home);
        deer.Needs.Hunger = 60f;

        world.Advance(1);

        var gatherTask = Assert.IsType<GatherTask>(deer.Tasks.Current);
        Assert.Same(fullOutsideHome, gatherTask.Target);
    }

    // WorldState.FindNearestGatherableEntity picks nearest-to-itself among nodes bounded by the
    // shared Home, not nearest-to-the-shared-anchor - the earlier anchor-centred search sent every
    // member of a herd at the single node nearest that one point, which starved the shipped map's
    // herds even with plenty of grass in aggregate (see DeerHerdMilestoneTests).
    [Fact]
    public void AHerdWithSeveralGrassNodesInsideItsHomeEndsUpGatheringFromMoreThanOneNode()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var home = NewHome(new Position(0, 0));

        // Four grass nodes spread around the anchor, each with a deer standing right beside it.
        var nodePositions = new[] { new Position(15, 0), new Position(-15, 0), new Position(0, 15), new Position(0, -15) };
        var deer = new List<Animal>();
        foreach (var position in nodePositions)
        {
            world.SpawnResourceNode(TestCatalogs.Grass, position, amount: 200f);
            var animal = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, position, home);
            animal.Needs.Hunger = 60f;
            deer.Add(animal);
        }

        world.Advance(1);

        var distinctTargets = deer.Select(animal => Assert.IsType<GatherTask>(animal.Tasks.Current).Target).Distinct().Count();
        Assert.True(distinctTargets > 1, $"expected more than one distinct grazing target, found {distinctTargets}");
    }

    [Fact]
    public void AHungryPersonWhoKnowsForagingDoesNotTreatGrassAsFood()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        world.SpawnResourceNode(TestCatalogs.Grass, position, amount: 200f);
        var person = world.SpawnPerson("Ava", position, TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        // A full pack: with no room to pick up grass as raw material either, the only way this
        // person could still end up beside the grass node is if it were mistakenly treated as
        // food (docs/todo/fauna-plan.md, step 0d).
        person.Inventory.Add(TestCatalogs.WoodItem, 100);
        person.Needs.Hunger = 80f;

        world.Advance(5);

        Assert.IsType<IdleTask>(person.Tasks.Current);
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.GrassItem));
    }
}
