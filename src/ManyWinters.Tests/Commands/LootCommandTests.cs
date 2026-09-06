using ManyWinters.Core.Commands;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class LootCommandTests
{
    [Fact]
    public void LootingTransfersTheWholeInventoryToTheLootingPerson()
    {
        var world = TestCatalogs.CreateWorld();
        var deceased = world.SpawnPerson("Ava", new Position(0, 0));
        deceased.IsAlive = false;
        deceased.Inventory.Add(TestCatalogs.WoodItem, 5);
        var looter = world.SpawnPerson("Bran", new Position(0, 0));

        world.Execute(new LootCommand(looter, deceased));

        Assert.Equal(0, deceased.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(5, looter.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void LootingWorksForAnyLivingPersonNotJustARelative()
    {
        var world = TestCatalogs.CreateWorld();
        var deceased = world.SpawnPerson("Ava", new Position(0, 0));
        deceased.IsAlive = false;
        deceased.Inventory.Add(TestCatalogs.WoodItem, 5);
        var bystander = world.SpawnPerson("Bystander", new Position(0, 0));

        world.Execute(new LootCommand(bystander, deceased));

        Assert.Equal(5, bystander.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void LootingRequiresTheLootingPersonToBeAlive()
    {
        var world = TestCatalogs.CreateWorld();
        var deceased = world.SpawnPerson("Ava", new Position(0, 0));
        deceased.IsAlive = false;
        deceased.Inventory.Add(TestCatalogs.WoodItem, 5);
        var otherDeceased = world.SpawnPerson("Bran", new Position(0, 0));
        otherDeceased.IsAlive = false;

        world.Execute(new LootCommand(otherDeceased, deceased));

        Assert.Equal(5, deceased.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void LootingRequiresTheDeceasedToActuallyBeDead()
    {
        var world = TestCatalogs.CreateWorld();
        var stillAlive = world.SpawnPerson("Ava", new Position(0, 0));
        stillAlive.Inventory.Add(TestCatalogs.WoodItem, 5);
        var looter = world.SpawnPerson("Bran", new Position(0, 0));

        world.Execute(new LootCommand(looter, stillAlive));

        Assert.Equal(5, stillAlive.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(0, looter.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void LootingAtExactlyTheMaxInteractionDistanceStillWorks()
    {
        var world = TestCatalogs.CreateWorld();
        var deceased = world.SpawnPerson("Ava", new Position(0, 0));
        deceased.IsAlive = false;
        deceased.Inventory.Add(TestCatalogs.WoodItem, 5);
        var looter = world.SpawnPerson("Bran", new Position(world.Configuration.Rules.MaxInteractionDistance, 0));

        world.Execute(new LootCommand(looter, deceased));

        Assert.Equal(5, looter.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void LootingBeyondTheMaxInteractionDistanceDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var deceased = world.SpawnPerson("Ava", new Position(0, 0));
        deceased.IsAlive = false;
        deceased.Inventory.Add(TestCatalogs.WoodItem, 5);
        var looter = world.SpawnPerson("Bran", new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0));

        world.Execute(new LootCommand(looter, deceased));

        Assert.Equal(5, deceased.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(0, looter.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void LootingStillWorksAfterTheDeceasedHasAlreadyBeenBuried()
    {
        var world = TestCatalogs.CreateWorld();
        var deceased = world.SpawnPerson("Ava", new Position(0, 0));
        deceased.IsAlive = false;
        deceased.IsBuried = true;
        deceased.Inventory.Add(TestCatalogs.WoodItem, 5);
        var looter = world.SpawnPerson("Bran", new Position(0, 0));

        world.Execute(new LootCommand(looter, deceased));

        Assert.Equal(5, looter.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void LootingOnlyTakesWhatStillFitsInTheLootersInventoryAndLeavesTheRestOnTheCorpse()
    {
        var world = TestCatalogs.CreateWorld();
        var deceased = world.SpawnPerson("Ava", new Position(0, 0));
        deceased.IsAlive = false;
        deceased.Inventory.Add(TestCatalogs.WoodItem, 20);
        var looter = world.SpawnPerson("Bran", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        looter.Inventory.Add(TestCatalogs.WoodItem, (int)world.MaxCarryWeightFor(looter) - 5);

        world.Execute(new LootCommand(looter, deceased));

        Assert.Equal(world.MaxCarryWeightFor(looter), looter.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(15, deceased.Inventory.Get(TestCatalogs.WoodItem));
    }
}
