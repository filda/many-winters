using ManyWinters.Core.Commands;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

// Idle hands working something out for themselves. What keeps knowledge living in people rather
// than in the player's head: a band left alone still develops, and one that comes after an
// extinction re-derives things instead of waiting to be shown (see
// docs/materials-and-crafting-architecture.md section 7).
public class IdleDiscoveryTests
{
    // Certain rather than rare, so a test about *what* gets discovered is not also a test of how
    // long it takes. The rate itself has its own tests below.
    private static WorldState WorldWhereIdlingAlwaysTeaches() =>
        new(TestCatalogs.CreateConfiguration() with
        {
            Rules = SimulationRules.Default with { IdleDiscoveryChancePerTick = 1f },
        });

    private static Person Idler(WorldState world, float curiosity = 1f)
    {
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks, curiosity: curiosity);
        person.Tasks.Interrupt(new IdleTask());

        return person;
    }

    private static Assembly.Part Cord() => new(new MaterialId("plant_fibre"), TestCatalogs.Cord, Quality: 0.5f, Volume: 15f);

    [Fact]
    public void SomebodyIdlingWithGrassEventuallyWorksOutHowToTwistIt()
    {
        var world = WorldWhereIdlingAlwaysTeaches();
        var person = Idler(world);
        person.Inventory.Add(TestCatalogs.GrassItem, 20);

        world.Advance(20);

        Assert.Contains(TestCatalogs.BasicTwisting, person.KnownTechniques);
    }

    // The rule is "what you could have done, if only you had known how" - so an empty pack
    // teaches nothing, however long somebody stands about.
    [Fact]
    public void IdlingEmptyHandedTeachesNothing()
    {
        var world = WorldWhereIdlingAlwaysTeaches();
        var person = Idler(world);

        world.Advance(50);

        Assert.Empty(person.KnownTechniques);
    }

    // Nothing in their hands affords twisting, so there is nothing there to work out.
    [Fact]
    public void IdlingWithSomethingThatTakesNoWorkingTeachesNothing()
    {
        var world = WorldWhereIdlingAlwaysTeaches();
        var person = Idler(world);
        person.Inventory.Add(TestCatalogs.WoodItem, 20);

        world.Advance(50);

        Assert.DoesNotContain(TestCatalogs.BasicTwisting, person.KnownTechniques);
    }

    [Fact]
    public void SomebodyIdlingWithTwoThingsAndACordWorksOutHowToBind()
    {
        var world = WorldWhereIdlingAlwaysTeaches();
        var person = Idler(world);
        person.Inventory.Add(TestCatalogs.WoodItem, 4);
        person.Inventory.Add(TestCatalogs.StoneItem, 4);
        person.Inventory.AddAssembly(Cord());

        world.Advance(40);

        Assert.Contains(TestCatalogs.BasicBinding, person.KnownTechniques);
    }

    // Busy hands are busy: somebody sent somewhere is doing that, not turning a thing over.
    [Fact]
    public void SomebodyWithSomethingElseToDoDiscoversNothing()
    {
        var world = WorldWhereIdlingAlwaysTeaches();
        var person = Idler(world);
        person.Inventory.Add(TestCatalogs.GrassItem, 20);
        person.Tasks.Interrupt(new MoveTask(new Position(500, 500), 0.01f));

        world.Advance(20);

        Assert.DoesNotContain(TestCatalogs.BasicTwisting, person.KnownTechniques);
    }

    [Fact]
    public void TheDeadWorkNothingOut()
    {
        var world = WorldWhereIdlingAlwaysTeaches();
        var person = Idler(world);
        person.Inventory.Add(TestCatalogs.GrassItem, 20);
        person.IsAlive = false;

        world.Advance(20);

        Assert.Empty(person.KnownTechniques);
    }

    // The knob an NPC band turns down (see Person.Curiosity): the same world, the same rules,
    // and a tribe that works things out more slowly than the player's own.
    [Fact]
    public void ALessCuriousBandWorksThingsOutMoreSlowlyInTheSameWorld()
    {
        var world = new WorldState(TestCatalogs.CreateConfiguration() with
        {
            Rules = SimulationRules.Default with { IdleDiscoveryChancePerTick = 0.3f },
        });

        var eager = Idler(world);
        var incurious = world.SpawnPerson("Bran", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks, curiosity: 0f);
        incurious.Tasks.Interrupt(new IdleTask());

        eager.Inventory.Add(TestCatalogs.GrassItem, 50);
        incurious.Inventory.Add(TestCatalogs.GrassItem, 50);

        world.Advance(60);

        Assert.Contains(TestCatalogs.BasicTwisting, eager.KnownTechniques);
        Assert.DoesNotContain(TestCatalogs.BasicTwisting, incurious.KnownTechniques);
    }

    // Non-zero, and small: a band left alone develops, but over winters rather than an
    // afternoon. Measured across a crowd rather than asserted of one person, because one
    // person's roll is a function of their own seed - a single idler landing it inside a season
    // is luck, a majority of them landing it is a rate that is too high.
    [Fact]
    public void TheShippedRateTakesWintersRatherThanAnAfternoon()
    {
        var world = TestCatalogs.CreateWorld();
        var idlers = new List<Person>();
        for (var i = 0; i < 30; i++)
        {
            var person = Idler(world, world.Configuration.Rules.StartingBandCuriosity);
            person.Inventory.Add(TestCatalogs.GrassItem, 500);
            idlers.Add(person);
        }

        world.Advance(world.Configuration.Rules.TicksPerSeason);

        var learned = idlers.Count(person => person.KnownTechniques.Contains(TestCatalogs.BasicTwisting));

        Assert.True(world.Configuration.Rules.IdleDiscoveryChancePerTick > 0f);
        Assert.True(learned < idlers.Count / 2, $"{learned} of {idlers.Count} worked twisting out within one season.");
    }

    // The player's own band is deliberately slow at this: what they work out alone is a floor
    // under somebody who has missed something, not a substitute for teaching them, which is the
    // game (see SimulationRules.StartingBandCuriosity).
    [Fact]
    public void TheShippedBandIsSlowerThanFullCuriosity()
    {
        Assert.InRange(SimulationRules.Default.StartingBandCuriosity, 0.0001f, 0.99f);
    }

    [Fact]
    public void SomebodySpawnedIntoTheWorldCarriesThePlayerBandsRate()
    {
        var world = TestCatalogs.CreateWorld();

        world.Execute(new SpawnPersonCommand("Ava", new Position(0, 0), Person.Unknown, Person.Unknown));

        Assert.Equal(world.Configuration.Rules.StartingBandCuriosity, world.People[^1].Curiosity);
    }

    // A child is of its mother's band, not of the player's: an NPC tribe's children stay on the
    // tribe's rate.
    [Fact]
    public void AChildWorksThingsOutAtItsMothersRate()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks, sex: Sex.Female, curiosity: 0.1f);
        var father = world.SpawnPerson("Bran", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks, sex: Sex.Male, curiosity: 0.1f);

        world.Execute(new BirthCommand("Cass", mother, father));

        Assert.Equal(0.1f, world.People[^1].Curiosity, 5);
    }
}
