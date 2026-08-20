using ManyWinters.Core.Commands;
using ManyWinters.Core.Construction;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class DepositCommandTests
{
    [Fact]
    public void DepositingMovesItemsFromPersonToBuilding()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 20);
        var building = world.AddBuilding(TestCatalogs.StorageHut, new Position(0, 0));

        world.Execute(new DepositCommand(person.Id, building.Id, TestCatalogs.WoodItem, 15));

        Assert.Equal(5, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(15, building.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void DepositingWithoutEnoughItemsDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);
        var building = world.AddBuilding(TestCatalogs.StorageHut, new Position(0, 0));

        world.Execute(new DepositCommand(person.Id, building.Id, TestCatalogs.WoodItem, 15));

        Assert.Equal(5, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(0, building.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void DepositingByADeadPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.IsAlive = false;
        person.Inventory.Add(TestCatalogs.WoodItem, 20);
        var building = world.AddBuilding(TestCatalogs.StorageHut, new Position(0, 0));

        world.Execute(new DepositCommand(person.Id, building.Id, TestCatalogs.WoodItem, 15));

        Assert.Equal(20, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(0, building.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void DepositingAtExactlyTheMaxInteractionDistanceStillWorks()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 20);
        var building = world.AddBuilding(TestCatalogs.StorageHut, new Position(WorldState.MaxInteractionDistance, 0));

        world.Execute(new DepositCommand(person.Id, building.Id, TestCatalogs.WoodItem, 15));

        Assert.Equal(15, building.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void DepositingBeyondTheMaxInteractionDistanceDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 20);
        var building = world.AddBuilding(TestCatalogs.StorageHut, new Position(WorldState.MaxInteractionDistance + 1, 0));

        world.Execute(new DepositCommand(person.Id, building.Id, TestCatalogs.WoodItem, 15));

        Assert.Equal(20, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(0, building.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void DepositingIntoAnUnknownBuildingDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 20);

        world.Execute(new DepositCommand(person.Id, new BuildingId(999), TestCatalogs.WoodItem, 15));

        Assert.Equal(20, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void DepositingByAnUnknownPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var building = world.AddBuilding(TestCatalogs.StorageHut, new Position(0, 0));

        world.Execute(new DepositCommand(new PersonId(999), building.Id, TestCatalogs.WoodItem, 15));

        Assert.Equal(0, building.Inventory.Get(TestCatalogs.WoodItem));
    }
}
