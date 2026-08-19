using ManyWinters.Core.Commands;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class CraftCommandTests
{
    [Fact]
    public void CraftingConsumesTheInputItemsAndProducesTheOutput()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.AxeInputAmount);

        world.Execute(new CraftCommand(person.Id, TestCatalogs.Axe));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(1, person.Inventory.Get(TestCatalogs.Axe));
    }

    [Fact]
    public void CraftingLeavesLeftoverInputItemsInInventory()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.AxeInputAmount + 3);

        world.Execute(new CraftCommand(person.Id, TestCatalogs.Axe));

        Assert.Equal(3, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(1, person.Inventory.Get(TestCatalogs.Axe));
    }

    [Fact]
    public void CraftingWithoutEnoughInputItemsDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.AxeInputAmount - 1);

        world.Execute(new CraftCommand(person.Id, TestCatalogs.Axe));

        Assert.Equal(TestCatalogs.AxeInputAmount - 1, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.Axe));
    }

    [Fact]
    public void CraftingByADeadPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.IsAlive = false;
        person.Inventory.Add(TestCatalogs.WoodItem, TestCatalogs.AxeInputAmount);

        world.Execute(new CraftCommand(person.Id, TestCatalogs.Axe));

        Assert.Equal(TestCatalogs.AxeInputAmount, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.Axe));
    }

    [Fact]
    public void CraftingForAnUnknownPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();

        world.Execute(new CraftCommand(new PersonId(999), TestCatalogs.Axe));
    }
}
