using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class GatherCommandTests
{
    [Fact]
    public void GatheringAddsToInventoryAndDepletesTheNode()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person.Id, node.Id));

        Assert.Equal(20, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(80f, node.RemainingAmount);
        Assert.Equal(1f, person.Skills.Get(TestCatalogs.Foraging));
    }

    [Fact]
    public void GatheringNeverTakesMoreThanTheNodeHasRemaining()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 5);

        world.Execute(new GatherCommand(person.Id, node.Id));

        Assert.Equal(5, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(0f, node.RemainingAmount);
    }

    [Fact]
    public void GatheringNeverTakesMoreThanStillFitsInTheInventory()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.AppleItem, (int)Person.MaxCarryWeight - 5);
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person.Id, node.Id));

        Assert.Equal(Person.MaxCarryWeight, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(95f, node.RemainingAmount);
    }

    [Fact]
    public void GatheringFromAnAlreadyEmptyNodeDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 0);

        world.Execute(new GatherCommand(person.Id, node.Id));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(0f, node.RemainingAmount);
        Assert.Equal(0f, person.Skills.Get(TestCatalogs.Foraging));
    }

    [Fact]
    public void GatheringFromAFelledNodeDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);
        world.Execute(new FellCommand(person.Id, node.Id));

        world.Execute(new GatherCommand(person.Id, node.Id));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(100f, node.RemainingAmount);
    }

    [Fact]
    public void GatheringByADeadPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.IsAlive = false;
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person.Id, node.Id));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(100f, node.RemainingAmount);
    }

    [Fact]
    public void GatheringAtExactlyTheMaxInteractionDistanceStillWorks()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(WorldState.MaxInteractionDistance, 0), 100);

        world.Execute(new GatherCommand(person.Id, node.Id));

        Assert.Equal(20, person.Inventory.Get(TestCatalogs.AppleItem));
    }

    [Fact]
    public void GatheringBeyondTheMaxInteractionDistanceDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(WorldState.MaxInteractionDistance + 1, 0), 100);

        world.Execute(new GatherCommand(person.Id, node.Id));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(100f, node.RemainingAmount);
    }

    [Fact]
    public void GatheringWithAnUnknownPersonOrNodeDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new GatherCommand(new PersonId(999), node.Id));
        world.Execute(new GatherCommand(person.Id, new ResourceNodeId(999)));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(100f, node.RemainingAmount);
    }

    [Fact]
    public void FiveAppleGathersDiscoverEfficientForaging()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 1000);

        for (var i = 0; i < 4; i++)
        {
            world.Execute(new GatherCommand(person.Id, node.Id));
        }

        Assert.DoesNotContain(TestCatalogs.EfficientForaging, person.KnownTechniques);

        world.Execute(new GatherCommand(person.Id, node.Id));

        Assert.Equal(5f, person.Skills.Get(TestCatalogs.Foraging));
        Assert.Contains(TestCatalogs.EfficientForaging, person.KnownTechniques);
    }

    [Fact]
    public void KnowingEfficientForagingHarvestsMorePerAction()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.EfficientForaging);
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 1000);

        world.Execute(new GatherCommand(person.Id, node.Id));

        Assert.Equal(40, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(960f, node.RemainingAmount);
    }

    [Fact]
    public void GatheringPearsAlsoTrainsTheForagingSkill()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var appleNode = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);
        var pearNode = world.AddResourceNode(TestCatalogs.Pear, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person.Id, appleNode.Id));
        world.Execute(new GatherCommand(person.Id, pearNode.Id));

        Assert.Equal(2f, person.Skills.Get(TestCatalogs.Foraging));
    }

    [Fact]
    public void GatheringMushroomsTrainsADifferentSkillThanForaging()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var appleNode = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);
        var mushroomNode = world.AddResourceNode(TestCatalogs.Mushroom, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person.Id, appleNode.Id));
        world.Execute(new GatherCommand(person.Id, mushroomNode.Id));

        Assert.Equal(1f, person.Skills.Get(TestCatalogs.Foraging));
        Assert.Equal(1f, person.Skills.Get(TestCatalogs.MushroomForaging));
    }

    [Fact]
    public void DiscoveringEfficientForagingDoesNotUnlockEfficientMushroomForaging()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 1000);

        for (var i = 0; i < 5; i++)
        {
            world.Execute(new GatherCommand(person.Id, node.Id));
        }

        Assert.Contains(TestCatalogs.EfficientForaging, person.KnownTechniques);
        Assert.DoesNotContain(TestCatalogs.EfficientMushroomForaging, person.KnownTechniques);
    }

    [Fact]
    public void GatheringWoodAddsItToInventoryInsteadOfReducingHunger()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Needs.Hunger = 50;
        var node = world.AddResourceNode(TestCatalogs.Wood, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person.Id, node.Id));

        Assert.Equal(50f, person.Needs.Hunger);
        Assert.Equal(80f, node.RemainingAmount);
        Assert.Equal(20, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void HavingAnAxeInInventoryHarvestsMoreWoodPerAction()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.Axe, 1);
        var node = world.AddResourceNode(TestCatalogs.Wood, new Position(0, 0), 1000);

        world.Execute(new GatherCommand(person.Id, node.Id));

        Assert.Equal(20 + TestCatalogs.AxeHarvestBonus, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(1000f - (20 + TestCatalogs.AxeHarvestBonus), node.RemainingAmount);
    }

    [Fact]
    public void TheAxeBonusDoesNotApplyToASkillWithNoAssociatedTool()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.Axe, 1);
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 1000);

        world.Execute(new GatherCommand(person.Id, node.Id));

        Assert.Equal(980f, node.RemainingAmount);
    }

    [Fact]
    public void GatheringInWinterYieldsLessForASeasonalResource()
    {
        var world = TestCatalogs.CreateWorld();
        world.Advance(225);
        var person = world.AddPerson("Ava", new Position(0, 0));
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person.Id, node.Id));

        Assert.Equal(Season.Winter, world.CurrentSeason);
        var expectedHarvest = 20f * TestCatalogs.ColdFoodYieldMultiplier;
        Assert.Equal((int)expectedHarvest, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(100f - expectedHarvest, node.RemainingAmount);
    }

    [Fact]
    public void GatheringWoodInWinterIsUnaffectedBySeasonalYield()
    {
        var world = TestCatalogs.CreateWorld();
        world.Advance(225);
        var person = world.AddPerson("Ava", new Position(0, 0));
        var node = world.AddResourceNode(TestCatalogs.Wood, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person.Id, node.Id));

        Assert.Equal(Season.Winter, world.CurrentSeason);
        Assert.Equal(20, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(80f, node.RemainingAmount);
    }
}
