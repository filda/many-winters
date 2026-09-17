using ManyWinters.Core.Commands;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class ConstructCommandTests
{
    [Fact]
    public void ConstructingConsumesTheRequiredItemsAndAddsTheBuilding()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount);
        var command = new ConstructCommand(person, TestCatalogs.StorageHut, new Position(1, 1));

        Assert.Equal(ActionBlocker.None, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.WoodItem));
        var building = Assert.Single(world.Entities, e => e.Category == EntityCategory.Building);
        Assert.Equal(TestCatalogs.StorageHut, building.Kind);
        Assert.Equal(new Position(1, 1), building.Position);
    }

    [Fact]
    public void ConstructingStartsTheBuildingAtFullConditionWithAnEmptyInventory()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount);

        world.Execute(new ConstructCommand(person, TestCatalogs.StorageHut, new Position(0, 0)));

        var building = Assert.Single(world.Entities, e => e.Category == EntityCategory.Building);
        Assert.Equal(100f, building.Condition);
        Assert.Empty(building.Storage!.Counts);
    }

    [Fact]
    public void ConstructingLeavesLeftoverInputItemsInInventory()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount + 3);

        world.Execute(new ConstructCommand(person, TestCatalogs.StorageHut, new Position(0, 0)));

        Assert.Equal(3, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Single(world.Entities, e => e.Category == EntityCategory.Building);
    }

    [Fact]
    public void ConstructingWithoutEnoughInputItemsDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount - 1);
        var command = new ConstructCommand(person, TestCatalogs.StorageHut, new Position(0, 0));

        Assert.Equal(ActionBlocker.MissingMaterials, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(TestCatalogs.StorageHutInputAmount - 1, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.DoesNotContain(world.Entities, e => e.Category == EntityCategory.Building);
    }

    [Fact]
    public void ConstructingByADeadPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.IsAlive = false;
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount);
        var command = new ConstructCommand(person, TestCatalogs.StorageHut, new Position(0, 0));

        Assert.Equal(ActionBlocker.ActorIsDead, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(TestCatalogs.StorageHutInputAmount, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.DoesNotContain(world.Entities, e => e.Category == EntityCategory.Building);
    }

    [Fact]
    public void ConstructingAtExactlyTheMaxInteractionDistanceStillWorks()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount);

        world.Execute(new ConstructCommand(person, TestCatalogs.StorageHut, new Position(world.Configuration.Rules.MaxInteractionDistance, 0)));

        Assert.Single(world.Entities, e => e.Category == EntityCategory.Building);
    }

    [Fact]
    public void ConstructingBeyondTheMaxInteractionDistanceDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount);
        var command = new ConstructCommand(person, TestCatalogs.StorageHut, new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0));

        Assert.Equal(ActionBlocker.TooFar, command.Blocker(world));
        world.Execute(command);

        Assert.DoesNotContain(world.Entities, e => e.Category == EntityCategory.Building);
        Assert.Equal(TestCatalogs.StorageHutInputAmount, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void NothingBlocksBuildingWithTheMaterialsInHand()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount);

        Assert.Equal(ActionBlocker.None, new ConstructCommand(person, TestCatalogs.StorageHut, new Position(0, 0)).Blocker(world));
    }

    [Fact]
    public void ADeadPersonIsBlockedFromBuilding()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount);
        person.IsAlive = false;

        Assert.Equal(ActionBlocker.ActorIsDead, new ConstructCommand(person, TestCatalogs.StorageHut, new Position(0, 0)).Blocker(world));
    }

    [Fact]
    public void ASiteOutOfReachBlocksTheBuildAsTooFar()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount);
        var site = new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0);

        Assert.Equal(ActionBlocker.TooFar, new ConstructCommand(person, TestCatalogs.StorageHut, site).Blocker(world));
    }

    [Fact]
    public void TooLittleWoodBlocksTheBuildAsMissingMaterials()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount - 1);

        Assert.Equal(ActionBlocker.MissingMaterials, new ConstructCommand(person, TestCatalogs.StorageHut, new Position(0, 0)).Blocker(world));
    }
}
