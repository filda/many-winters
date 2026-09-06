using ManyWinters.Core.Commands;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class WithdrawCommandTests
{
    [Fact]
    public void WithdrawingEverythingThatFitsLeavesNoEmptyEntryInTheBuilding()
    {
        // Nothing goes back when it all fit, so the building's store has to end up genuinely
        // empty - a zero-count entry reads as "there's wood in here" to anything listing it.
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        building.Inventory.Add(TestCatalogs.WoodItem, 5);

        world.Execute(new WithdrawCommand(person, building, TestCatalogs.WoodItem, 5));

        Assert.Equal(5, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Empty(building.Inventory.Counts);
    }

    [Fact]
    public void WithdrawingMovesItemsFromBuildingToPerson()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        building.Inventory.Add(TestCatalogs.WoodItem, 20);

        world.Execute(new WithdrawCommand(person, building, TestCatalogs.WoodItem, 15));

        Assert.Equal(5, building.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(15, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void WithdrawingWithoutEnoughItemsDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        building.Inventory.Add(TestCatalogs.WoodItem, 5);

        world.Execute(new WithdrawCommand(person, building, TestCatalogs.WoodItem, 15));

        Assert.Equal(5, building.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void WithdrawingByADeadPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.IsAlive = false;
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        building.Inventory.Add(TestCatalogs.WoodItem, 20);

        world.Execute(new WithdrawCommand(person, building, TestCatalogs.WoodItem, 15));

        Assert.Equal(20, building.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void WithdrawingAtExactlyTheMaxInteractionDistanceStillWorks()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(world.Configuration.Rules.MaxInteractionDistance, 0));
        building.Inventory.Add(TestCatalogs.WoodItem, 20);

        world.Execute(new WithdrawCommand(person, building, TestCatalogs.WoodItem, 15));

        Assert.Equal(15, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void WithdrawingBeyondTheMaxInteractionDistanceDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0));
        building.Inventory.Add(TestCatalogs.WoodItem, 20);

        world.Execute(new WithdrawCommand(person, building, TestCatalogs.WoodItem, 15));

        Assert.Equal(20, building.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void WithdrawingOnlyTakesWhatStillFitsInThePersonsInventoryAndLeavesTheRestInTheBuilding()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.Inventory.Add(TestCatalogs.WoodItem, (int)world.MaxCarryWeightFor(person) - 5);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        building.Inventory.Add(TestCatalogs.WoodItem, 20);

        world.Execute(new WithdrawCommand(person, building, TestCatalogs.WoodItem, 15));

        Assert.Equal(world.MaxCarryWeightFor(person), person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(15, building.Inventory.Get(TestCatalogs.WoodItem));
    }
}
