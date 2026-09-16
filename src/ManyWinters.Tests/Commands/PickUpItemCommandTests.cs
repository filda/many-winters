using ManyWinters.Core.Commands;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class PickUpItemCommandTests
{
    [Fact]
    public void PickingUpTransfersTheWholePileToTheInventoryAndRemovesIt()
    {
        var world = TestCatalogs.CreateWorld();
        var pile = world.SpawnItemPile(TestCatalogs.WoodItem, new Position(0, 0), 5);
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        world.Execute(new PickUpItemCommand(person, pile));

        Assert.Equal(5, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Empty(world.ItemPiles);
    }

    [Fact]
    public void PickingUpOnlyTakesWhatStillFitsAndLeavesTheRestOnThePile()
    {
        var world = TestCatalogs.CreateWorld();
        var pile = world.SpawnItemPile(TestCatalogs.WoodItem, new Position(0, 0), 20);
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.Inventory.Add(TestCatalogs.WoodItem, (int)world.MaxCarryWeightFor(person) - 5);

        world.Execute(new PickUpItemCommand(person, pile));

        Assert.Equal(world.MaxCarryWeightFor(person), person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(15, pile.Amount);
        Assert.Single(world.ItemPiles);
    }

    [Fact]
    public void ADeadPersonCannotPickUp()
    {
        var world = TestCatalogs.CreateWorld();
        var pile = world.SpawnItemPile(TestCatalogs.WoodItem, new Position(0, 0), 5);
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.IsAlive = false;

        world.Execute(new PickUpItemCommand(person, pile));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(5, pile.Amount);
    }

    [Fact]
    public void PickingUpBeyondTheMaxInteractionDistanceDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var pile = world.SpawnItemPile(TestCatalogs.WoodItem, new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0), 5);
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        world.Execute(new PickUpItemCommand(person, pile));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(5, pile.Amount);
    }

    [Fact]
    public void NothingBlocksPickingUpAPileWithinReach()
    {
        var world = TestCatalogs.CreateWorld();
        var pile = world.SpawnItemPile(TestCatalogs.WoodItem, new Position(0, 0), 5);
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        Assert.Equal(ActionBlocker.None, new PickUpItemCommand(person, pile).Blocker(world));
    }

    [Fact]
    public void ADeadActorIsBlockedFromPickingUp()
    {
        var world = TestCatalogs.CreateWorld();
        var pile = world.SpawnItemPile(TestCatalogs.WoodItem, new Position(0, 0), 5);
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.IsAlive = false;

        Assert.Equal(ActionBlocker.ActorIsDead, new PickUpItemCommand(person, pile).Blocker(world));
    }

    [Fact]
    public void APileOutOfReachBlocksPickingUpAsTooFar()
    {
        var world = TestCatalogs.CreateWorld();
        var pile = world.SpawnItemPile(TestCatalogs.WoodItem, new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0), 5);
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        Assert.Equal(ActionBlocker.TooFar, new PickUpItemCommand(person, pile).Blocker(world));
    }

    [Fact]
    public void AnEmptiedPileBlocksPickingUpAsTargetIsGone()
    {
        var world = TestCatalogs.CreateWorld();
        var pile = world.SpawnItemPile(TestCatalogs.WoodItem, new Position(0, 0), 0);
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        Assert.Equal(ActionBlocker.TargetIsGone, new PickUpItemCommand(person, pile).Blocker(world));
    }
}
