using ManyWinters.Core.Commands;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class MakeCommandTests
{
    // Light output: fits in the pack (an axe), so it lands there regardless of any position
    // given - the routing is derived from weight, not from the caller.
    [Fact]
    public void MakingSomethingLightAddsItToInventory()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.AxeInputAmount);

        world.Execute(new MakeCommand(person, TestCatalogs.Axe));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(1, person.Inventory.Get(TestCatalogs.Axe));
        Assert.Empty(world.Entities);
    }

    [Fact]
    public void MakingSomethingLightLeavesLeftoverInputItemsInInventory()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.AxeInputAmount + 3);

        world.Execute(new MakeCommand(person, TestCatalogs.Axe));

        Assert.Equal(3, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(1, person.Inventory.Get(TestCatalogs.Axe));
    }

    [Fact]
    public void MakingSomethingLightWithoutEnoughInputItemsDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.AxeInputAmount - 1);

        world.Execute(new MakeCommand(person, TestCatalogs.Axe));

        Assert.Equal(TestCatalogs.AxeInputAmount - 1, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.Axe));
    }

    [Fact]
    public void MakingByADeadPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.IsAlive = false;
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.AxeInputAmount);

        world.Execute(new MakeCommand(person, TestCatalogs.Axe));

        Assert.Equal(TestCatalogs.AxeInputAmount, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.Axe));
    }

    [Fact]
    public void NothingBlocksMakingSomethingLightWithTheMaterialsInHand()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.AxeInputAmount);

        Assert.Equal(ActionBlocker.None, new MakeCommand(person, TestCatalogs.Axe).Blocker(world));
    }

    [Fact]
    public void TooFewInputItemsBlocksTheMakeAsMissingMaterials()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.AxeInputAmount - 1);

        Assert.Equal(ActionBlocker.MissingMaterials, new MakeCommand(person, TestCatalogs.Axe).Blocker(world));
    }

    [Fact]
    public void ADeadPersonIsBlockedFromMaking()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.AxeInputAmount);
        person.IsAlive = false;

        Assert.Equal(ActionBlocker.ActorIsDead, new MakeCommand(person, TestCatalogs.Axe).Blocker(world));
    }

    // Heavy output: never fits in the pack, so it lands in the world instead - at the given
    // position, or at the maker's own feet if none was given.
    [Fact]
    public void MakingSomethingHeavyPlacesItInTheWorldAtTheGivenPosition()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount);
        var command = new MakeCommand(person, TestCatalogs.StorageHutItem, new Position(1, 1));

        Assert.Equal(ActionBlocker.None, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.StorageHutItem));
        var building = Assert.Single(world.Entities, e => e.Category == EntityCategory.Building);
        Assert.Equal(TestCatalogs.StorageHut, building.Kind);
        Assert.Equal(new Position(1, 1), building.Position);
    }

    [Fact]
    public void MakingSomethingHeavyWithoutAPositionPlacesItAtTheMakersFeet()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(3, 4));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount);

        world.Execute(new MakeCommand(person, TestCatalogs.StorageHutItem));

        var building = Assert.Single(world.Entities, e => e.Category == EntityCategory.Building);
        Assert.Equal(new Position(3, 4), building.Position);
    }

    [Fact]
    public void MakingSomethingHeavyStartsItAtFullConditionWithAnEmptyStore()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount);

        world.Execute(new MakeCommand(person, TestCatalogs.StorageHutItem, new Position(0, 0)));

        var building = Assert.Single(world.Entities, e => e.Category == EntityCategory.Building);
        Assert.Equal(100f, building.Condition);
        Assert.Empty(building.Storage!.Counts);
    }

    [Fact]
    public void MakingSomethingHeavyLeavesLeftoverInputItemsInInventory()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount + 3);

        world.Execute(new MakeCommand(person, TestCatalogs.StorageHutItem, new Position(0, 0)));

        Assert.Equal(3, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Single(world.Entities, e => e.Category == EntityCategory.Building);
    }

    [Fact]
    public void MakingSomethingHeavyWithoutEnoughInputItemsDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount - 1);
        var command = new MakeCommand(person, TestCatalogs.StorageHutItem, new Position(0, 0));

        Assert.Equal(ActionBlocker.MissingMaterials, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(TestCatalogs.StorageHutInputAmount - 1, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Empty(world.Entities);
    }

    [Fact]
    public void MakingSomethingHeavyByADeadPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.IsAlive = false;
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount);
        var command = new MakeCommand(person, TestCatalogs.StorageHutItem, new Position(0, 0));

        Assert.Equal(ActionBlocker.ActorIsDead, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(TestCatalogs.StorageHutInputAmount, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Empty(world.Entities);
    }

    [Fact]
    public void MakingSomethingHeavyAtExactlyTheMaxInteractionDistanceStillWorks()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount);

        world.Execute(new MakeCommand(person, TestCatalogs.StorageHutItem, new Position(world.Configuration.Rules.MaxInteractionDistance, 0)));

        Assert.Single(world.Entities, e => e.Category == EntityCategory.Building);
    }

    [Fact]
    public void MakingSomethingHeavyBeyondTheMaxInteractionDistanceDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount);
        var command = new MakeCommand(person, TestCatalogs.StorageHutItem, new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0));

        Assert.Equal(ActionBlocker.TooFar, command.Blocker(world));
        world.Execute(command);

        Assert.Empty(world.Entities);
        Assert.Equal(TestCatalogs.StorageHutInputAmount, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void NothingBlocksMakingSomethingHeavyWithTheMaterialsInHandAtASpotWithinReach()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount);

        Assert.Equal(ActionBlocker.None, new MakeCommand(person, TestCatalogs.StorageHutItem, new Position(0, 0)).Blocker(world));
    }

    [Fact]
    public void ASiteOutOfReachBlocksTheMakeAsTooFar()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount);
        var site = new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0);

        Assert.Equal(ActionBlocker.TooFar, new MakeCommand(person, TestCatalogs.StorageHutItem, site).Blocker(world));
    }

    [Fact]
    public void TooLittleWoodBlocksTheHeavyMakeAsMissingMaterials()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount - 1);

        Assert.Equal(ActionBlocker.MissingMaterials, new MakeCommand(person, TestCatalogs.StorageHutItem, new Position(0, 0)).Blocker(world));
    }
}
