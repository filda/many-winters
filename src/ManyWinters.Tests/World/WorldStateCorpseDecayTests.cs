using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

// docs/todo/fauna-plan.md phase 4 ("Zpracování") and phase 4c: a dead, unburied Animal's bones
// vanish SimulationRules.BonesLingerTicks after SimulationRules.CorpseDecayTicks - a Person's
// never do, since the record of a band is its graves. What perishable *goods* a corpse holds no
// longer follows CorpseDecayTicks at all (that was 4a's rule): meat, rawhide and the rest now rot
// on each material's own shelf life, wherever they lie - see WorldStateSpoilageTests.
public class WorldStateCorpseDecayTests
{
    private static HomeRange NewHome(Position anchor) => new(anchor) { Radius = 10f, DriftMetresPerSeason = 0f };

    // Meat and rawhide rot on their own clocks - meat first, since it has the shorter shelf life -
    // while bone and (since phase 4c) sinew, which no longer spoils, outlast both.
    [Fact]
    public void ADeadAnimalsMeatAndRawhideRotOnTheirOwnClocksWhileBoneAndSinewOutlastThem()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var catalog = world.Configuration.ItemCatalog;
        var deer = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(0, 0), NewHome(new Position(0, 0)));
        deer.IsAlive = false;
        deer.DeathTick = 0;
        deer.Inventory.Add(TestCatalogs.MeatItem, TestCatalogs.DeerCarcassMeat, tick: 0, catalog);
        deer.Inventory.Add(TestCatalogs.RawhideItem, TestCatalogs.DeerCarcassHide, tick: 0, catalog);
        deer.Inventory.Add(TestCatalogs.BoneItem, TestCatalogs.DeerCarcassBone, tick: 0, catalog);
        deer.Inventory.Add(TestCatalogs.SinewItem, TestCatalogs.DeerCarcassSinew, tick: 0, catalog);

        world.Advance(TestCatalogs.MeatShelfLifeTicks - 1);
        Assert.Equal(TestCatalogs.DeerCarcassMeat, deer.Inventory.Get(TestCatalogs.MeatItem));
        Assert.Equal(TestCatalogs.DeerCarcassHide, deer.Inventory.Get(TestCatalogs.RawhideItem));

        world.Advance(1);
        Assert.Equal(0, deer.Inventory.Get(TestCatalogs.MeatItem));
        // Rawhide's own, longer shelf life hasn't come due yet.
        Assert.Equal(TestCatalogs.DeerCarcassHide, deer.Inventory.Get(TestCatalogs.RawhideItem));

        world.Advance(TestCatalogs.RawhideShelfLifeTicks - TestCatalogs.MeatShelfLifeTicks);
        Assert.Equal(0, deer.Inventory.Get(TestCatalogs.RawhideItem));

        Assert.Equal(TestCatalogs.DeerCarcassBone, deer.Inventory.Get(TestCatalogs.BoneItem));
        Assert.Equal(TestCatalogs.DeerCarcassSinew, deer.Inventory.Get(TestCatalogs.SinewItem));
    }

    [Fact]
    public void ADeadPersonsFoodRotsButTheirToolsStay()
    {
        var world = TestCatalogs.CreateWorld();
        var catalog = world.Configuration.ItemCatalog;
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.IsAlive = false;
        person.DeathTick = 0;
        person.Inventory.Add(TestCatalogs.AppleItem, 5, tick: 0, catalog);
        person.Inventory.Add(TestCatalogs.Axe, 1);
        person.Inventory.Add(TestCatalogs.WoodItem, 3);

        world.Advance(TestCatalogs.AppleShelfLifeTicks);

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(1, person.Inventory.Get(TestCatalogs.Axe));
        Assert.Equal(3, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void AnUnburiedDeadAnimalIsRemovedOnceItsBonesHaveLingeredAndAnimalRemovedFiresOnce()
    {
        var world = TestCatalogs.CreateWorldWithShortCorpseDecay(corpseDecayTicks: 10, bonesLingerTicks: 10);
        var deer = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(0, 0), NewHome(new Position(0, 0)));
        deer.IsAlive = false;
        deer.DeathTick = 0;
        deer.Inventory.Add(TestCatalogs.BoneItem, TestCatalogs.DeerCarcassBone);
        var removedCount = 0;
        Animal? removed = null;
        world.AnimalRemoved += animal =>
        {
            removedCount++;
            removed = animal;
        };

        var threshold = world.Configuration.Rules.CorpseDecayTicks + world.Configuration.Rules.BonesLingerTicks;
        world.Advance(threshold - 1);

        Assert.Contains(deer, world.Animals);
        Assert.Equal(0, removedCount);

        world.Advance(1);

        Assert.DoesNotContain(deer, world.Animals);
        Assert.Same(deer, removed);
        Assert.Equal(1, removedCount);

        // Further advancing must not fire it again - the animal is gone, not merely marked.
        world.Advance(50);
        Assert.Equal(1, removedCount);
    }

    [Fact]
    public void ADeadPersonIsNeverRemovedEvenLongAfterBonesWouldHaveLingered()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.IsAlive = false;
        person.DeathTick = 0;

        world.Advance(world.Configuration.Rules.CorpseDecayTicks + world.Configuration.Rules.BonesLingerTicks + 10);

        Assert.Contains(person, world.People);
    }

    [Fact]
    public void IsDecayedIsFalseBeforeTheThresholdAndTrueAtAndAfterIt()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.IsAlive = false;
        person.DeathTick = 0;

        world.Clock.Advance(world.Configuration.Rules.CorpseDecayTicks - 1);
        Assert.False(world.IsDecayed(person));

        world.Clock.Advance();
        Assert.True(world.IsDecayed(person));
    }

    [Fact]
    public void ALivingCreatureIsNeverDecayed()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        world.Clock.Advance(world.Configuration.Rules.CorpseDecayTicks + 10);

        Assert.False(world.IsDecayed(person));
    }
}
