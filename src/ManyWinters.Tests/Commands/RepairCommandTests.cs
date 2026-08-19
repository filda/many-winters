using ManyWinters.Core.Commands;
using ManyWinters.Core.Construction;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class RepairCommandTests
{
    [Fact]
    public void RepairingRestoresConditionAndConsumesAQuarterOfTheBuildCost()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);
        var building = world.AddBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        building.Condition = 50f;

        world.Execute(new RepairCommand(person.Id, building.Id));

        Assert.Equal(75f, building.Condition);
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void RepairingCapsConditionAtItsMaximum()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);
        var building = world.AddBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        building.Condition = 90f;

        world.Execute(new RepairCommand(person.Id, building.Id));

        Assert.Equal(100f, building.Condition);
    }

    [Fact]
    public void RepairingAnAlreadyFullConditionBuildingDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);
        var building = world.AddBuilding(TestCatalogs.StorageHut, new Position(0, 0));

        world.Execute(new RepairCommand(person.Id, building.Id));

        Assert.Equal(100f, building.Condition);
        Assert.Equal(5, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void RepairingWithoutEnoughMaterialsDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 4);
        var building = world.AddBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        building.Condition = 50f;

        world.Execute(new RepairCommand(person.Id, building.Id));

        Assert.Equal(50f, building.Condition);
        Assert.Equal(4, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void RepairingByADeadPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.IsAlive = false;
        person.Inventory.Add(TestCatalogs.WoodItem, 5);
        var building = world.AddBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        building.Condition = 50f;

        world.Execute(new RepairCommand(person.Id, building.Id));

        Assert.Equal(50f, building.Condition);
    }

    [Fact]
    public void RepairingAnUnknownBuildingDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);

        world.Execute(new RepairCommand(person.Id, new BuildingId(999)));

        Assert.Equal(5, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void RepairingByAnUnknownPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var building = world.AddBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        building.Condition = 50f;

        world.Execute(new RepairCommand(new PersonId(999), building.Id));

        Assert.Equal(50f, building.Condition);
    }
}
