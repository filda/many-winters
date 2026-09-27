using ManyWinters.Core.Commands;
using ManyWinters.Core.Items;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

// docs/todo/fauna-plan.md phase 4c: the once-per-tick spoilage pass in WorldState.Advance -
// wherever a perishable thing lies (a ground pile, a building's storage, a pack carried between
// containers), it vanishes SimulationRules... no, its own material's ShelfLifeTicks after it came
// to be, full stop. Inventory's own tests (InventoryTests) cover the ledger/transfer mechanics
// directly; these are about WorldState actually running that pass over the whole map.
public class WorldStateSpoilageTests
{
    [Fact]
    public void ADroppedPileOfAPerishableStockVanishesOnceItsShelfLifeIsUp()
    {
        var world = TestCatalogs.CreateWorld();
        var catalog = world.Configuration.ItemCatalog;
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        // Ticked, not the plain untimed Add every other test's setup uses: an untimed unit is
        // deliberately exempt from Expire (docs/todo/fauna-plan.md phase 4c), so a pile built from
        // one would never vanish - this test is about the case that does.
        person.Inventory.Add(TestCatalogs.AppleItem, 5, tick: 0, catalog);
        world.Execute(new DropCommand(person, new CarriedThing.Stock(TestCatalogs.AppleItem, 5)));
        var pile = Assert.Single(world.Entities, e => e.Category == EntityCategory.Pile);

        world.Advance(TestCatalogs.AppleShelfLifeTicks - 1);
        Assert.Contains(pile, world.Entities);

        world.Advance(1);
        Assert.DoesNotContain(pile, world.Entities);
    }

    [Fact]
    public void ADroppedPileOfANonPerishableStockNeverVanishesOnItsOwn()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);
        world.Execute(new DropCommand(person, new CarriedThing.Stock(TestCatalogs.WoodItem, 5)));
        var pile = Assert.Single(world.Entities, e => e.Category == EntityCategory.Pile);

        world.Advance(TestCatalogs.AppleShelfLifeTicks * 10);

        Assert.Contains(pile, world.Entities);
    }

    // Dropping is not a transfer the way Deposit/Withdraw/Loot/Butcher/PickUp are - a pile holds
    // one tick for however many units land in it - but it must not be a way to launder a
    // nearly-spoiled stack back to fresh either: the pile takes the OLDEST tick among what was
    // dropped (Inventory.RemoveDated), and PickUpItemCommand carries that same tick back into the
    // pack, so the round trip changes nothing about when the meat spoils.
    [Fact]
    public void DroppingAndPickingUpMeatPreservesItsOriginalAgeRatherThanRefreshingIt()
    {
        var world = TestCatalogs.CreateWorld();
        var catalog = world.Configuration.ItemCatalog;
        var position = new Position(0, 0);
        var person = world.SpawnPerson("Ava", position);
        person.Inventory.Add(TestCatalogs.MeatItem, 5, tick: 0, catalog);

        world.Advance(20);
        // An untasked person idly wanders (IdleTask) - pinned back so the reach checks below are
        // about spoilage, not about where an unrelated random walk happened to leave them.
        person.Position = position;
        world.Execute(new DropCommand(person, new CarriedThing.Stock(TestCatalogs.MeatItem, 5)));
        var pile = Assert.Single(world.Entities, e => e.Category == EntityCategory.Pile);

        world.Advance(5);
        person.Position = position;
        world.Execute(new PickUpItemCommand(person, pile));
        Assert.Equal(25, world.Clock.CurrentTick);
        Assert.Equal(5, person.Inventory.Get(TestCatalogs.MeatItem));

        // Picked at tick 0, so it spoils at tick 30 - not at 25 + its own shelf life (55), which
        // stamping the pile (or the pickup) "now" would give instead.
        world.Advance(TestCatalogs.MeatShelfLifeTicks - 25 - 1);
        Assert.Equal(5, person.Inventory.Get(TestCatalogs.MeatItem));

        world.Advance(1);
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.MeatItem));
    }

    // Deposit then withdraw is a transfer at each end (docs/todo/fauna-plan.md phase 4c): meat
    // butchered (well, handed straight into a pack here) at tick 0, deposited at tick 10 and
    // withdrawn at tick 20 spoils at tick 30 - not at tick 20 + its own shelf life, as restamping
    // it at each hand-off would give.
    [Fact]
    public void MeatKeepsItsOriginalAgeAcrossADepositAndAWithdraw()
    {
        var world = TestCatalogs.CreateWorld();
        var catalog = world.Configuration.ItemCatalog;
        var position = new Position(0, 0);
        var person = world.SpawnPerson("Ava", position);
        var hut = MakeStorageHut(world, position);
        person.Inventory.Add(TestCatalogs.MeatItem, 5, tick: 0, catalog);

        world.Advance(10);
        // An untasked person idly wanders (IdleTask) - pinned back so the reach checks below are
        // about spoilage, not about where an unrelated random walk happened to leave them.
        person.Position = position;
        world.Execute(new DepositCommand(person, hut, new CarriedThing.Stock(TestCatalogs.MeatItem, 5)));

        world.Advance(10);
        person.Position = position;
        world.Execute(new WithdrawCommand(person, hut, new CarriedThing.Stock(TestCatalogs.MeatItem, 5)));

        Assert.Equal(20, world.Clock.CurrentTick);
        Assert.Equal(5, person.Inventory.Get(TestCatalogs.MeatItem));

        world.Advance(TestCatalogs.MeatShelfLifeTicks - 20 - 1);
        Assert.Equal(5, person.Inventory.Get(TestCatalogs.MeatItem));

        world.Advance(1);
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.MeatItem));
    }

    private static Entity MakeStorageHut(WorldState world, Position position)
    {
        var hut = new Entity
        {
            Kind = TestCatalogs.StorageHut,
            Category = EntityCategory.Building,
            Position = position,
            Condition = 100f,
            Storage = new Inventory(),
        };
        world.AddEntity(hut);
        return hut;
    }
}
