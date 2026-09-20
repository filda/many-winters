using ManyWinters.Core.Commands;
using ManyWinters.Core.Materials;
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

    // What somebody made outlives them. Until worked things could be taken off a body, a winter
    // of practice went into the ground with whoever was holding it (see
    // docs/materials-and-crafting-architecture.md, the instance tier).
    private static Assembly.Part Axehead(float volume = 1f) =>
        new(new MaterialId("stone"), TestCatalogs.Wedge, 1f, volume);

    [Fact]
    public void LootingTakesWhatTheyMadeAsWellAsWhatTheyGathered()
    {
        var world = TestCatalogs.CreateWorld();
        var deceased = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        deceased.IsAlive = false;
        deceased.Inventory.AddAssembly(Axehead());
        deceased.Inventory.Add(TestCatalogs.WoodItem, 2);
        var looter = world.SpawnPerson("Bran", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);

        world.Execute(new LootCommand(looter, deceased));

        Assert.Empty(deceased.Inventory.Assemblies);
        Assert.Equal(Axehead(), Assert.Single(looter.Inventory.Assemblies));
        Assert.Equal(2, looter.Inventory.Get(TestCatalogs.WoodItem));
    }

    // A made thing is the one thing the band cannot go and gather again, so a pack that will not
    // hold everything holds that first.
    [Fact]
    public void AMadeThingComesOffTheBodyBeforeTheFirewood()
    {
        var world = TestCatalogs.CreateWorld();
        var deceased = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        deceased.IsAlive = false;
        deceased.Inventory.AddAssembly(Axehead());
        var looter = world.SpawnPerson("Bran", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);

        // Far more wood than anyone can carry, so the pack is full long before the list ends.
        deceased.Inventory.Add(TestCatalogs.WoodItem, 500);

        world.Execute(new LootCommand(looter, deceased));

        Assert.Equal(Axehead(), Assert.Single(looter.Inventory.Assemblies));
        Assert.True(deceased.Inventory.Get(TestCatalogs.WoodItem) > 0);
    }

    // Taken whole or not at all: half an axe is nothing.
    [Fact]
    public void AMadeThingTooHeavyToCarryStaysOnTheBody()
    {
        var world = TestCatalogs.CreateWorld();
        var deceased = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        deceased.IsAlive = false;
        var millstone = Axehead(volume: 1000f);
        deceased.Inventory.AddAssembly(millstone);
        var looter = world.SpawnPerson("Bran", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);

        world.Execute(new LootCommand(looter, deceased));

        Assert.Equal(millstone, Assert.Single(deceased.Inventory.Assemblies));
        Assert.Empty(looter.Inventory.Assemblies);
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

    [Fact]
    public void NothingBlocksLootingACorpseWithinReach()
    {
        var world = TestCatalogs.CreateWorld();
        var looter = world.SpawnPerson("Bran", new Position(0, 0));
        var deceased = world.SpawnPerson("Ava", new Position(0, 0));
        deceased.IsAlive = false;

        Assert.Equal(ActionBlocker.None, new LootCommand(looter, deceased).Blocker(world));
    }

    [Fact]
    public void ADeadLooterIsBlockedFromLooting()
    {
        var world = TestCatalogs.CreateWorld();
        var looter = world.SpawnPerson("Bran", new Position(0, 0));
        looter.IsAlive = false;
        var deceased = world.SpawnPerson("Ava", new Position(0, 0));
        deceased.IsAlive = false;

        Assert.Equal(ActionBlocker.ActorIsDead, new LootCommand(looter, deceased).Blocker(world));
    }

    [Fact]
    public void ALivingTargetBlocksTheLoot()
    {
        var world = TestCatalogs.CreateWorld();
        var looter = world.SpawnPerson("Bran", new Position(0, 0));
        var living = world.SpawnPerson("Ava", new Position(0, 0));

        Assert.Equal(ActionBlocker.TargetIsAlive, new LootCommand(looter, living).Blocker(world));
    }

    [Fact]
    public void ACorpseOutOfReachBlocksTheLootAsTooFar()
    {
        var world = TestCatalogs.CreateWorld();
        var looter = world.SpawnPerson("Bran", new Position(0, 0));
        var deceased = world.SpawnPerson("Ava", new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0));
        deceased.IsAlive = false;

        Assert.Equal(ActionBlocker.TooFar, new LootCommand(looter, deceased).Blocker(world));
    }
}
