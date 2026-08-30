using ManyWinters.Core.Commands;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class LootCommandTests
{
    [Fact]
    public void LootingTransfersTheWholeInventoryToTheLootingPerson()
    {
        var world = new WorldState();
        var deceased = world.AddPerson("Ava", new Position(0, 0));
        deceased.IsAlive = false;
        deceased.Inventory.Add(TestCatalogs.WoodItem, 5);
        var looter = world.AddPerson("Bran", new Position(0, 0));

        world.Execute(new LootCommand(looter.Id, deceased.Id));

        Assert.Equal(0, deceased.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(5, looter.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void LootingWorksForAnyLivingPersonNotJustARelative()
    {
        var world = new WorldState();
        var deceased = world.AddPerson("Ava", new Position(0, 0));
        deceased.IsAlive = false;
        deceased.Inventory.Add(TestCatalogs.WoodItem, 5);
        var bystander = world.AddPerson("Bystander", new Position(0, 0));

        world.Execute(new LootCommand(bystander.Id, deceased.Id));

        Assert.Equal(5, bystander.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void LootingRequiresTheLootingPersonToBeAlive()
    {
        var world = new WorldState();
        var deceased = world.AddPerson("Ava", new Position(0, 0));
        deceased.IsAlive = false;
        deceased.Inventory.Add(TestCatalogs.WoodItem, 5);
        var otherDeceased = world.AddPerson("Bran", new Position(0, 0));
        otherDeceased.IsAlive = false;

        world.Execute(new LootCommand(otherDeceased.Id, deceased.Id));

        Assert.Equal(5, deceased.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void LootingRequiresTheDeceasedToActuallyBeDead()
    {
        var world = new WorldState();
        var stillAlive = world.AddPerson("Ava", new Position(0, 0));
        stillAlive.Inventory.Add(TestCatalogs.WoodItem, 5);
        var looter = world.AddPerson("Bran", new Position(0, 0));

        world.Execute(new LootCommand(looter.Id, stillAlive.Id));

        Assert.Equal(5, stillAlive.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(0, looter.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void LootingAtExactlyTheMaxInteractionDistanceStillWorks()
    {
        var world = new WorldState();
        var deceased = world.AddPerson("Ava", new Position(0, 0));
        deceased.IsAlive = false;
        deceased.Inventory.Add(TestCatalogs.WoodItem, 5);
        var looter = world.AddPerson("Bran", new Position(WorldState.MaxInteractionDistance, 0));

        world.Execute(new LootCommand(looter.Id, deceased.Id));

        Assert.Equal(5, looter.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void LootingBeyondTheMaxInteractionDistanceDoesNothing()
    {
        var world = new WorldState();
        var deceased = world.AddPerson("Ava", new Position(0, 0));
        deceased.IsAlive = false;
        deceased.Inventory.Add(TestCatalogs.WoodItem, 5);
        var looter = world.AddPerson("Bran", new Position(WorldState.MaxInteractionDistance + 1, 0));

        world.Execute(new LootCommand(looter.Id, deceased.Id));

        Assert.Equal(5, deceased.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(0, looter.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void LootingWithUnknownPersonIdsDoesNothing()
    {
        var world = new WorldState();
        var deceased = world.AddPerson("Ava", new Position(0, 0));
        deceased.IsAlive = false;
        deceased.Inventory.Add(TestCatalogs.WoodItem, 5);

        world.Execute(new LootCommand(new PersonId(999), deceased.Id));
        world.Execute(new LootCommand(new PersonId(998), new PersonId(999)));

        Assert.Equal(5, deceased.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void LootingStillWorksAfterTheDeceasedHasAlreadyBeenBuried()
    {
        var world = new WorldState();
        var deceased = world.AddPerson("Ava", new Position(0, 0));
        deceased.IsAlive = false;
        deceased.IsBuried = true;
        deceased.Inventory.Add(TestCatalogs.WoodItem, 5);
        var looter = world.AddPerson("Bran", new Position(0, 0));

        world.Execute(new LootCommand(looter.Id, deceased.Id));

        Assert.Equal(5, looter.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void LootingOnlyTakesWhatStillFitsInTheLootersInventoryAndLeavesTheRestOnTheCorpse()
    {
        var world = TestCatalogs.CreateWorld();
        var deceased = world.AddPerson("Ava", new Position(0, 0));
        deceased.IsAlive = false;
        deceased.Inventory.Add(TestCatalogs.WoodItem, 20);
        var looter = world.AddPerson("Bran", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        looter.Inventory.Add(TestCatalogs.WoodItem, (int)world.MaxCarryWeightFor(looter) - 5);

        world.Execute(new LootCommand(looter.Id, deceased.Id));

        Assert.Equal(world.MaxCarryWeightFor(looter), looter.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(15, deceased.Inventory.Get(TestCatalogs.WoodItem));
    }
}
