using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

// A deer herd grazes when hungry, wanders otherwise, and starves or dies of old age like
// anyone else.
public class AnimalSimulationTests
{
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

        // Already at the deer's MaxLifespanYears (8) - the next tick tips it into old age.
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

    // Whatever the cause - hunger here, old age above - a dead deer's carcass fills exactly once,
    // with exactly the species' numbers.
    [Fact]
    public void ADeerThatStarvesLeavesExactlyOneCarcassWorthOfMaterialsInItsInventory()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        var deer = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, position, NewHome(position));

        world.Advance(400);

        // Meat and rawhide are long gone by now: they rot on much shorter clocks. Bone and sinew
        // never spoil, so they are still exactly the carcass's numbers here - which is what
        // "fills exactly once, not topped up again and again" over 400 ticks proves.
        Assert.False(deer.IsAlive);
        Assert.Equal(TestCatalogs.DeerCarcassBone, deer.Inventory.Get(TestCatalogs.BoneItem));
        Assert.Equal(TestCatalogs.DeerCarcassSinew, deer.Inventory.Get(TestCatalogs.SinewItem));

        // Ticks keep advancing after death (the herd around it is still alive) - the carcass
        // must not be topped up again and again.
        world.Advance(50);

        Assert.Equal(TestCatalogs.DeerCarcassBone, deer.Inventory.Get(TestCatalogs.BoneItem));
        Assert.Equal(TestCatalogs.DeerCarcassSinew, deer.Inventory.Get(TestCatalogs.SinewItem));
    }

    // People are not butchered: a human's species carries no Carcass, so a dead person's
    // Inventory gets nothing added by dying, whatever they died of - LootCommand remains the
    // only way to take their possessions.
    [Fact]
    public void AHumansDeathAddsNothingToTheirInventory()
    {
        var world = new WorldState(TestCatalogs.CreateConfigurationWithLifeCycle(new LifeCycle(1, 4, 7, 4)));
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: SimulationRules.Default.TicksPerYear * 4);

        world.Advance(1);

        Assert.False(person.IsAlive);
        Assert.Equal(DeathCause.OldAge, person.CauseOfDeath);
        Assert.Empty(person.Inventory.Counts);
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

        // The gather this same tick executes as eating, not pocketing; this one only pins the
        // task itself.
        Assert.IsType<GatherTask>(deer.Tasks.Current);
    }

    // The in-home food search only counts a node that would still give this deer a full harvest -
    // a node down to a sliver still passes the plain "more than zero left" check, so without this
    // a herd would nibble its barely-regrown home tuft at regen speed forever rather than falling
    // through to fuller grass further out.
    [Fact]
    public void AHerdWhoseHomeNodesAreNearlyEmptyGoesToTheFullerGrassOutsideTheHome()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var homeCenter = new Position(0, 0);
        var home = NewHome(homeCenter, radius: 15f);

        var nearlyEmptyInsideHome = world.SpawnResourceNode(TestCatalogs.Grass, new Position(2, 0), amount: 200f);
        nearlyEmptyInsideHome.Growth!.RemainingAmount = 1f;
        var fullOutsideHome = world.SpawnResourceNode(TestCatalogs.Grass, new Position(40, 0), amount: 200f);

        // Standing well out toward the edge of its territory, close to the outside node -
        // both tiers pick nearest-to-the-deer-itself, not nearest-to-the-anchor, so this is what
        // lets the outside node win over the anchor-hugging depleted one once the home tier has
        // ruled the depleted one out.
        var deer = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(35, 0), home);
        deer.Needs.Hunger = 60f;

        world.Advance(1);

        var gatherTask = Assert.IsType<GatherTask>(deer.Tasks.Current);
        Assert.Same(fullOutsideHome, gatherTask.Target);
    }

    // The food search picks nearest-to-itself among nodes bounded by the shared home, not
    // nearest-to-the-shared-anchor - the earlier anchor-centred search sent every member of a
    // herd at the single node nearest that one point, which starved the shipped map's herds even
    // with plenty of grass in aggregate.
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

    // The winter reserve: each species scales the base hunger-per-tick rate by its own
    // multiplier. Human is 1, deer mirrors the multiplier from deer.json.
    [Fact]
    public void APersonsHungerAccruesAtTheUnscaledRateWhileADeersIsScaledByItsSpecies()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var rules = world.Configuration.Rules;
        // Tick 0 is Spring (Mild, hunger multiplier 1) and far apart enough, with nothing edible
        // nearby, that neither creature's task or diet affects hunger.
        var person = world.SpawnPerson("Ava", new Position(-500, -500), TestCatalogs.AdultAgeTicks);
        var deer = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(500, 500), NewHome(new Position(500, 500)));

        world.Advance(3);

        Assert.Equal(rules.HungerPerTick * 3, person.Needs.Hunger, precision: 4);
        Assert.Equal(rules.HungerPerTick * 3 * TestCatalogs.DeerHungerPerTickMultiplier, deer.Needs.Hunger, precision: 4);
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
        // food.
        person.Inventory.Add(TestCatalogs.WoodItem, 100);
        person.Needs.Hunger = 80f;

        world.Advance(5);

        Assert.IsType<IdleTask>(person.Tasks.Current);
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.GrassItem));
    }

    private static HomeRange NewHome(Position anchor, float radius = 20f) => new(anchor) { Radius = radius, DriftMetresPerSeason = 0f };
}
