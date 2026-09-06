using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class GatherCommandTests
{
    [Fact]
    public void GatheringAResourceThatYieldsNoItemEatsStraightFromItInsteadOfFillingTheInventory()
    {
        // Nothing in the shipped content works this way yet, but a resource with no YieldsItem
        // (a berry patch grazed on the spot, a spring drunk from) is the one case where the
        // harvest relieves hunger directly rather than going into the backpack first.
        var grazing = new ResourceKindId("grazing");
        var configuration = TestCatalogs.CreateConfiguration() with
        {
            ResourceCatalog = new ResourceCatalog([new ResourceDefinition(grazing, "Grazing", TestCatalogs.Foraging)]),
        };
        var world = new WorldState(configuration);
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.Needs.Hunger = 50f;
        var node = world.SpawnResourceNode(grazing, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(30f, person.Needs.Hunger);
        Assert.Equal(80f, node.RemainingAmount);
        Assert.Empty(person.Inventory.Counts);
    }

    [Fact]
    public void GatheringAResourceThatYieldsNoItemNeverDrivesHungerBelowZero()
    {
        var grazing = new ResourceKindId("grazing");
        var configuration = TestCatalogs.CreateConfiguration() with
        {
            ResourceCatalog = new ResourceCatalog([new ResourceDefinition(grazing, "Grazing", TestCatalogs.Foraging)]),
        };
        var world = new WorldState(configuration);
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.Needs.Hunger = 5f;
        var node = world.SpawnResourceNode(grazing, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(0f, person.Needs.Hunger);
    }

    [Fact]
    public void GatheringAddsToInventoryAndDepletesTheNode()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(20, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(80f, node.RemainingAmount);
        Assert.Equal(1f, person.Skills.Get(TestCatalogs.Foraging));
    }

    [Fact]
    public void GatheringNeverTakesMoreThanTheNodeHasRemaining()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 5);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(5, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(0f, node.RemainingAmount);
    }

    [Fact]
    public void GatheringNeverTakesMoreThanStillFitsInTheInventory()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.Inventory.Add(TestCatalogs.AppleItem, (int)world.MaxCarryWeightFor(person) - 5);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(world.MaxCarryWeightFor(person), person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(95f, node.RemainingAmount);
    }

    [Fact]
    public void GatheringFromAnAlreadyEmptyNodeDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 0);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(0f, node.RemainingAmount);
        Assert.Equal(0f, person.Skills.Get(TestCatalogs.Foraging));
    }

    [Fact]
    public void GatheringFromAFelledNodeDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);
        world.Execute(new FellCommand(person, node));

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(100f, node.RemainingAmount);
    }

    [Fact]
    public void GatheringWithoutHavingLearnedTheSkillDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(100f, node.RemainingAmount);
    }

    [Fact]
    public void GatheringByADeadPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.IsAlive = false;
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(100f, node.RemainingAmount);
    }

    [Fact]
    public void GatheringAtExactlyTheMaxInteractionDistanceStillWorks()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(world.Configuration.Rules.MaxInteractionDistance, 0), 100);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(20, person.Inventory.Get(TestCatalogs.AppleItem));
    }

    [Fact]
    public void GatheringBeyondTheMaxInteractionDistanceDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0), 100);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(100f, node.RemainingAmount);
    }

    [Fact]
    public void FiveAppleGathersDiscoverEfficientForaging()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 1000);

        for (var i = 0; i < 4; i++)
        {
            GatherAndUnload(world, person, node);
        }

        Assert.DoesNotContain(TestCatalogs.EfficientForaging, person.KnownTechniques);

        GatherAndUnload(world, person, node);

        Assert.Equal(5f, person.Skills.Get(TestCatalogs.Foraging));
        Assert.Contains(TestCatalogs.EfficientForaging, person.KnownTechniques);
    }

    [Fact]
    public void KnowingEfficientForagingHarvestsMorePerAction()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.KnownTechniques.Add(TestCatalogs.EfficientForaging);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 1000);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(40, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(960f, node.RemainingAmount);
    }

    [Fact]
    public void GatheringPearsAlsoTrainsTheForagingSkill()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var appleNode = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);
        var pearNode = world.SpawnResourceNode(TestCatalogs.Pear, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person, appleNode));
        world.Execute(new GatherCommand(person, pearNode));

        Assert.Equal(2f, person.Skills.Get(TestCatalogs.Foraging));
    }

    [Fact]
    public void GatheringMushroomsTrainsADifferentSkillThanForaging()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.KnownTechniques.Add(TestCatalogs.BasicMushroomForaging);
        var appleNode = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);
        var mushroomNode = world.SpawnResourceNode(TestCatalogs.Mushroom, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person, appleNode));
        world.Execute(new GatherCommand(person, mushroomNode));

        Assert.Equal(1f, person.Skills.Get(TestCatalogs.Foraging));
        Assert.Equal(1f, person.Skills.Get(TestCatalogs.MushroomForaging));
    }

    [Fact]
    public void DiscoveringEfficientForagingDoesNotUnlockEfficientMushroomForaging()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 1000);

        for (var i = 0; i < 5; i++)
        {
            GatherAndUnload(world, person, node);
        }

        Assert.Contains(TestCatalogs.EfficientForaging, person.KnownTechniques);
        Assert.DoesNotContain(TestCatalogs.EfficientMushroomForaging, person.KnownTechniques);
    }

    // Five trips' worth of apples don't fit in one backpack, and a trip that brings nothing
    // back teaches nothing (see GatheringIntoAFullBackpackTakesNothingAndEarnsNoPractice) - so
    // the harvest is set down between trips, the way it would be at camp.
    private static void GatherAndUnload(WorldState world, Person person, ResourceNode node)
    {
        world.Execute(new GatherCommand(person, node));
        person.Inventory.Remove(TestCatalogs.AppleItem, person.Inventory.Get(TestCatalogs.AppleItem));
    }

    [Fact]
    public void GatheringWoodAddsItToInventoryInsteadOfReducingHunger()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);
        person.Needs.Hunger = 50;
        var node = world.SpawnResourceNode(TestCatalogs.Wood, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(50f, person.Needs.Hunger);
        Assert.Equal(80f, node.RemainingAmount);
        Assert.Equal(20, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void HavingAnAxeInInventoryHarvestsMoreWoodPerAction()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);
        person.Inventory.Add(TestCatalogs.Axe, 1);
        var node = world.SpawnResourceNode(TestCatalogs.Wood, new Position(0, 0), 1000);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(20 + TestCatalogs.AxeHarvestBonus, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(1000f - (20 + TestCatalogs.AxeHarvestBonus), node.RemainingAmount);
    }

    [Fact]
    public void TheAxeBonusDoesNotApplyToASkillWithNoAssociatedTool()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.Inventory.Add(TestCatalogs.Axe, 1);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 1000);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(980f, node.RemainingAmount);
    }

    [Fact]
    public void GatheringInWinterYieldsLessForASeasonalResource()
    {
        var world = TestCatalogs.CreateWorld();
        world.Advance(225);
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person, node));

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
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);
        var node = world.SpawnResourceNode(TestCatalogs.Wood, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(Season.Winter, world.CurrentSeason);
        Assert.Equal(20, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(80f, node.RemainingAmount);
    }

    [Fact]
    public void ADeadPersonGathersNothingEvenWithTheSkillAndTheNodeInReach()
    {
        // Every other reason to refuse is removed here - the skill is known, the node is alive
        // and full, and they are standing on it - so being dead is on its own what stops it.
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);
        person.IsAlive = false;

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(100f, node.RemainingAmount);
    }

    [Fact]
    public void AFelledNodeYieldsNothingToALivingSkilledGathererStandingOnIt()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);
        node.IsAlive = false;

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(100f, node.RemainingAmount);
    }

    [Fact]
    public void AnOutOfReachNodeYieldsNothingToALivingSkilledGatherer()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(50, 0), 100);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(100f, node.RemainingAmount);
    }

    [Fact]
    public void AHungryPickerWhoKnowsHowToEatEatsFromTheHarvestFirstAndPocketsTheRest()
    {
        // Twenty apples come off the tree; five go straight into the mouth (hunger 5, one hunger
        // per apple), the other fifteen into the backpack - and the tree is down by all twenty.
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 5f;
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(0f, person.Needs.Hunger);
        Assert.Equal(15, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(80f, node.RemainingAmount);
    }

    [Fact]
    public void EatingFromTheHarvestTrainsEatingAsWellAsTheGatheringSkill()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 5f;
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(1f, person.Skills.Get(TestCatalogs.Foraging));
        Assert.Equal(1f, person.Skills.Get(EatCommand.Skill));
    }

    [Fact]
    public void AHungryPickerWithAFullBackpackStillGetsFedAtAFoodSource()
    {
        // The backpack is full of wood, so nothing can be pocketed - but a hungry person who
        // knows how to eat still eats on the spot, and only what was eaten comes off the tree.
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 8f;
        FillTheBackpackWithWood(world, person);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(0f, person.Needs.Hunger);
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(92f, node.RemainingAmount);
    }

    [Fact]
    public void AHungryPickerWhoNeverLearnedToEatPocketsTheWholeHarvestInstead()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.Needs.Hunger = 5f;
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(5f, person.Needs.Hunger);
        Assert.Equal(20, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(80f, node.RemainingAmount);
    }

    [Fact]
    public void APickerWhoIsNotHungryPocketsTheWholeHarvest()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(20, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(0f, person.Skills.Get(EatCommand.Skill));
    }

    [Fact]
    public void GrassIsNeverEatenOnTheSpotHoweverHungryThePickerIs()
    {
        // Grass restores nothing, so "eating as you go" doesn't apply - it all goes into the
        // backpack and the hunger stays exactly where it was.
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 60f;
        var node = world.SpawnResourceNode(TestCatalogs.Grass, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(60f, person.Needs.Hunger);
        Assert.Equal(20, person.Inventory.Get(TestCatalogs.GrassItem));
        Assert.Equal(80f, node.RemainingAmount);
    }

    [Fact]
    public void GatheringIntoAFullBackpackTakesNothingAndEarnsNoPractice()
    {
        // Coming away with nothing is not gathering: the node is untouched and the skill stays
        // where it was, so a full backpack cannot grind out the efficient technique.
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        FillTheBackpackWithWood(world, person);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(100f, node.RemainingAmount);
        Assert.Equal(0f, person.Skills.Get(TestCatalogs.Foraging));
    }

    private static void FillTheBackpackWithWood(WorldState world, Person person)
    {
        var woodWeight = world.Configuration.ItemCatalog.WeightFor(TestCatalogs.WoodItem);
        person.Inventory.Add(TestCatalogs.WoodItem, (int)Math.Ceiling(world.MaxCarryWeightFor(person) / woodWeight));
    }
}
