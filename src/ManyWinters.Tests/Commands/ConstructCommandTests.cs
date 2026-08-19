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
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount);

        world.Execute(new ConstructCommand(person.Id, TestCatalogs.StorageHut, new Position(3, 4)));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.WoodItem));
        var building = Assert.Single(world.Buildings);
        Assert.Equal(TestCatalogs.StorageHut, building.Kind);
        Assert.Equal(new Position(3, 4), building.Position);
    }

    [Fact]
    public void ConstructingLeavesLeftoverInputItemsInInventory()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount + 3);

        world.Execute(new ConstructCommand(person.Id, TestCatalogs.StorageHut, new Position(0, 0)));

        Assert.Equal(3, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Single(world.Buildings);
    }

    [Fact]
    public void ConstructingWithoutEnoughInputItemsDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount - 1);

        world.Execute(new ConstructCommand(person.Id, TestCatalogs.StorageHut, new Position(0, 0)));

        Assert.Equal(TestCatalogs.StorageHutInputAmount - 1, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Empty(world.Buildings);
    }

    [Fact]
    public void ConstructingByADeadPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.IsAlive = false;
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.StorageHutInputAmount);

        world.Execute(new ConstructCommand(person.Id, TestCatalogs.StorageHut, new Position(0, 0)));

        Assert.Equal(TestCatalogs.StorageHutInputAmount, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Empty(world.Buildings);
    }

    [Fact]
    public void ConstructingForAnUnknownPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();

        world.Execute(new ConstructCommand(new PersonId(999), TestCatalogs.StorageHut, new Position(0, 0)));

        Assert.Empty(world.Buildings);
    }
}
