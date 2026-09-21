using ManyWinters.Core.Commands;
using ManyWinters.Core.Materials;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class DepositCommandTests
{
    [Fact]
    public void DepositingMovesItemsFromPersonToBuilding()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 20);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        var command = new DepositCommand(person, building, new CarriedThing.Stock(TestCatalogs.WoodItem, 15));

        Assert.Equal(ActionBlocker.None, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(5, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(15, building.Storage!.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void DepositingWithoutEnoughItemsDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        var command = new DepositCommand(person, building, new CarriedThing.Stock(TestCatalogs.WoodItem, 15));

        Assert.Equal(ActionBlocker.MissingMaterials, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(5, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(0, building.Storage!.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void DepositingByADeadPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.IsAlive = false;
        person.Inventory.Add(TestCatalogs.WoodItem, 20);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        var command = new DepositCommand(person, building, new CarriedThing.Stock(TestCatalogs.WoodItem, 15));

        Assert.Equal(ActionBlocker.ActorIsDead, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(20, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(0, building.Storage!.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void DepositingAtExactlyTheMaxInteractionDistanceStillWorks()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 20);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(world.Configuration.Rules.MaxInteractionDistance, 0));

        world.Execute(new DepositCommand(person, building, new CarriedThing.Stock(TestCatalogs.WoodItem, 15)));

        Assert.Equal(15, building.Storage!.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void DepositingBeyondTheMaxInteractionDistanceDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 20);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0));
        var command = new DepositCommand(person, building, new CarriedThing.Stock(TestCatalogs.WoodItem, 15));

        Assert.Equal(ActionBlocker.TooFar, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(20, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(0, building.Storage!.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void NothingBlocksADepositWithTheItemsInHand()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));

        Assert.Equal(ActionBlocker.None, new DepositCommand(person, building, new CarriedThing.Stock(TestCatalogs.WoodItem, 5)).Blocker(world));
    }

    [Fact]
    public void ADeadPersonIsBlockedFromDepositing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);
        person.IsAlive = false;
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));

        Assert.Equal(ActionBlocker.ActorIsDead, new DepositCommand(person, building, new CarriedThing.Stock(TestCatalogs.WoodItem, 5)).Blocker(world));
    }

    [Fact]
    public void ABuildingOutOfReachBlocksTheDepositAsTooFar()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0));

        Assert.Equal(ActionBlocker.TooFar, new DepositCommand(person, building, new CarriedThing.Stock(TestCatalogs.WoodItem, 5)).Blocker(world));
    }

    [Fact]
    public void CarryingTooFewOfTheItemBlocksTheDepositAsMissingMaterials()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 4);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));

        Assert.Equal(ActionBlocker.MissingMaterials, new DepositCommand(person, building, new CarriedThing.Stock(TestCatalogs.WoodItem, 5)).Blocker(world));
    }

    // Both tiers go on the shelves. A store holds an inventory as a pack does, so nothing about
    // putting a made thing away is a special case.
    private static Assembly.Part Cord() => new(new MaterialId("plant_fibre"), TestCatalogs.Cord, 0.8f, 5f);

    [Fact]
    public void SomethingTheyMadeGoesOnTheShelves()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        var cord = Cord();
        person.Inventory.AddAssembly(cord);

        world.Execute(new DepositCommand(person, building, new CarriedThing.Worked(cord)));

        Assert.Empty(person.Inventory.Assemblies);
        Assert.Equal(cord, Assert.Single(building.Storage!.Assemblies));
    }

    // A store has no capacity of its own, so nothing is turned away at the door.
    [Fact]
    public void AStoreTakesSomethingNobodyCouldCarryFar()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        var millstone = new Assembly.Part(new MaterialId("stone"), TestCatalogs.Wedge, 1f, 1000f);
        person.Inventory.AddAssembly(millstone);

        world.Execute(new DepositCommand(person, building, new CarriedThing.Worked(millstone)));

        Assert.Equal(millstone, Assert.Single(building.Storage!.Assemblies));
    }

    [Fact]
    public void PuttingAwaySomethingTheyAreNotCarryingIsBlocked()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));

        var command = new DepositCommand(person, building, new CarriedThing.Worked(Cord()));

        Assert.Equal(ActionBlocker.MissingMaterials, command.Blocker(world));
        world.Execute(command);
        Assert.Empty(building.Storage!.Assemblies);
    }
}
