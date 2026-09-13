using ManyWinters.Core.Commands;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class RepairCommandTests
{
    [Fact]
    public void RepairingRestoresConditionAndConsumesAQuarterOfTheBuildCost()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        building.Condition = 50f;

        world.Execute(new RepairCommand(person, building));

        Assert.Equal(75f, building.Condition);
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void RepairingCapsConditionAtItsMaximum()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        building.Condition = 90f;

        world.Execute(new RepairCommand(person, building));

        Assert.Equal(100f, building.Condition);
    }

    [Fact]
    public void RepairingAnAlreadyFullConditionBuildingDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));

        world.Execute(new RepairCommand(person, building));

        Assert.Equal(100f, building.Condition);
        Assert.Equal(5, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void RepairingWithoutEnoughMaterialsDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 4);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        building.Condition = 50f;

        world.Execute(new RepairCommand(person, building));

        Assert.Equal(50f, building.Condition);
        Assert.Equal(4, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void RepairingByADeadPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.IsAlive = false;
        person.Inventory.Add(TestCatalogs.WoodItem, 5);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        building.Condition = 50f;

        world.Execute(new RepairCommand(person, building));

        Assert.Equal(50f, building.Condition);
    }

    [Fact]
    public void RepairingAtExactlyTheMaxInteractionDistanceStillWorks()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(world.Configuration.Rules.MaxInteractionDistance, 0));
        building.Condition = 50f;

        world.Execute(new RepairCommand(person, building));

        Assert.Equal(75f, building.Condition);
    }

    [Fact]
    public void RepairingBeyondTheMaxInteractionDistanceDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0));
        building.Condition = 50f;

        world.Execute(new RepairCommand(person, building));

        Assert.Equal(50f, building.Condition);
        Assert.Equal(5, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void NothingBlocksRepairingADamagedBuildingWithWoodInHand()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        building.Condition = 40f;

        Assert.Equal(ActionBlocker.None, new RepairCommand(person, building).Blocker(world));
    }

    [Fact]
    public void ADeadPersonIsBlockedFromRepairing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount);
        person.IsAlive = false;
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        building.Condition = 40f;

        Assert.Equal(ActionBlocker.ActorIsDead, new RepairCommand(person, building).Blocker(world));
    }

    [Fact]
    public void AnUndamagedBuildingBlocksTheRepairAsNothingToRepair()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));

        Assert.Equal(ActionBlocker.NothingToRepair, new RepairCommand(person, building).Blocker(world));
    }

    [Fact]
    public void ABuildingOutOfReachBlocksTheRepairAsTooFar()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0));
        building.Condition = 40f;

        Assert.Equal(ActionBlocker.TooFar, new RepairCommand(person, building).Blocker(world));
    }

    [Fact]
    public void NoWoodInHandBlocksTheRepairAsMissingMaterials()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        building.Condition = 40f;

        Assert.Equal(ActionBlocker.MissingMaterials, new RepairCommand(person, building).Blocker(world));
    }
}
