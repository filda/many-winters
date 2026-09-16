using ManyWinters.Core.Commands;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class DropItemCommandTests
{
    [Fact]
    public void DroppingMovesItemsFromInventoryToANewPileAtThePersonsPosition()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(3, 4));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);

        world.Execute(new DropItemCommand(person, TestCatalogs.WoodItem, 3));

        Assert.Equal(2, person.Inventory.Get(TestCatalogs.WoodItem));
        var pile = Assert.Single(world.ItemPiles);
        Assert.Equal(TestCatalogs.WoodItem, pile.Kind);
        Assert.Equal(3, pile.Amount);
        Assert.Equal(person.Position, pile.Position);
    }

    [Fact]
    public void DroppingMoreThanCarriedDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 2);

        world.Execute(new DropItemCommand(person, TestCatalogs.WoodItem, 3));

        Assert.Equal(2, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Empty(world.ItemPiles);
    }

    [Fact]
    public void ADeadPersonCannotDropItems()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);
        person.IsAlive = false;

        world.Execute(new DropItemCommand(person, TestCatalogs.WoodItem, 3));

        Assert.Equal(5, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Empty(world.ItemPiles);
    }

    [Fact]
    public void NothingBlocksDroppingItemsActuallyCarried()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);

        Assert.Equal(ActionBlocker.None, new DropItemCommand(person, TestCatalogs.WoodItem, 3).Blocker(world));
    }

    [Fact]
    public void ADeadActorIsBlockedFromDropping()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 5);
        person.IsAlive = false;

        Assert.Equal(ActionBlocker.ActorIsDead, new DropItemCommand(person, TestCatalogs.WoodItem, 3).Blocker(world));
    }

    [Fact]
    public void DroppingMoreThanCarriedIsBlockedAsMissingMaterials()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WoodItem, 2);

        Assert.Equal(ActionBlocker.MissingMaterials, new DropItemCommand(person, TestCatalogs.WoodItem, 3).Blocker(world));
    }
}
