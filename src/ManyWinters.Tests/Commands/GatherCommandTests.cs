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
        // No shipped resource works this way yet; a resource with no YieldsItem is the one case
        // where the harvest relieves hunger directly instead of going into the backpack.
        var grazing = new EntityKindId("grazing");
        var configuration = TestCatalogs.CreateConfiguration() with
        {
            ResourceCatalog = new ResourceCatalog([new ResourceDefinition(grazing, "Grazing", TestCatalogs.Foraging)]),
        };
        var world = new WorldState(configuration);
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.Needs.Hunger = 50f;
        var node = world.SpawnResourceNode(grazing, new Position(0, 0), 100);
        var command = new GatherCommand(person, node);

        Assert.Equal(ActionBlocker.None, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(30f, person.Needs.Hunger);
        Assert.Equal(80f, node.Growth!.RemainingAmount);
        Assert.Empty(person.Inventory.Counts);
    }

    [Fact]
    public void GatheringAResourceThatYieldsNoItemNeverDrivesHungerBelowZero()
    {
        var grazing = new EntityKindId("grazing");
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
        var command = new GatherCommand(person, node);

        Assert.Equal(ActionBlocker.None, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(20, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(80f, node.Growth!.RemainingAmount);
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
        Assert.Equal(0f, node.Growth!.RemainingAmount);
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
        Assert.Equal(95f, node.Growth!.RemainingAmount);
    }

    [Fact]
    public void GatheringFromAnAlreadyEmptyNodeDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 0);
        var command = new GatherCommand(person, node);

        Assert.Equal(ActionBlocker.NothingLeft, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(0f, node.Growth!.RemainingAmount);
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
        Assert.Equal(100f, node.Growth!.RemainingAmount);
    }

    [Fact]
    public void GatheringWithoutHavingLearnedTheSkillDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);
        var command = new GatherCommand(person, node);

        Assert.Equal(ActionBlocker.NotLearned, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(100f, node.Growth!.RemainingAmount);
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
        Assert.Equal(100f, node.Growth!.RemainingAmount);
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
        Assert.Equal(100f, node.Growth!.RemainingAmount);
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

        // Practice has diminishing returns: five gathers leave the skill just over 2.5, where
        // the discovery threshold sits.
        Assert.Equal(2.553f, person.Skills.Get(TestCatalogs.Foraging), 3);
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
        Assert.Equal(960f, node.Growth!.RemainingAmount);
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

        // Both gathers train foraging; the second teaches half what the first did (1 + 1/2).
        Assert.Equal(1.5f, person.Skills.Get(TestCatalogs.Foraging));
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

    [Fact]
    public void ABarelyHungryPickerPocketsTheWholeHarvestRatherThanNibblingAtIt()
    {
        // Below the hunger-eat threshold the harvest goes into the backpack untouched, so
        // standing at a food source is not a way to eat (and practice eating) every tick.
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 5f;
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(5f, person.Needs.Hunger);
        Assert.Equal(20, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(0f, person.Skills.Get(EatCommand.Skill));
    }

    private static WorldState CreateWorldWhereAnyHungerIsWorthEating() =>
        new(TestCatalogs.CreateConfiguration() with { Rules = SimulationRules.Default with { HungerEatThreshold = 1f } });

    // Five trips' worth of apples don't fit in one backpack, and a trip that brings nothing back
    // teaches nothing, so the harvest is set down between trips.
    private static void GatherAndUnload(WorldState world, Person person, Entity node)
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
        Assert.Equal(80f, node.Growth!.RemainingAmount);
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
        var expectedHarvest = (int)(20 + world.Configuration.ItemCatalog.ChoppingScoreFor(TestCatalogs.Axe));

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(expectedHarvest, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(1000f - expectedHarvest, node.Growth!.RemainingAmount);
    }

    [Fact]
    public void TheChoppingBonusDoesNotApplyToASkillThatDoesNotUseIt()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.Inventory.Add(TestCatalogs.Axe, 1);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 1000);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(980f, node.Growth!.RemainingAmount);
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
        Assert.Equal(100f - expectedHarvest, node.Growth!.RemainingAmount);
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
        Assert.Equal(80f, node.Growth!.RemainingAmount);
    }

    [Fact]
    public void ADeadPersonGathersNothingEvenWithTheSkillAndTheNodeInReach()
    {
        // Every other reason to refuse is removed, so being dead is on its own what stops it.
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);
        person.IsAlive = false;

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(100f, node.Growth!.RemainingAmount);
    }

    [Fact]
    public void AFelledNodeYieldsNothingToALivingSkilledGathererStandingOnIt()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);
        node.Growth!.IsAlive = false;

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(100f, node.Growth!.RemainingAmount);
    }

    [Fact]
    public void AnOutOfReachNodeYieldsNothingToALivingSkilledGatherer()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(50, 0), 100);
        var command = new GatherCommand(person, node);

        Assert.Equal(ActionBlocker.TooFar, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(100f, node.Growth!.RemainingAmount);
    }

    [Fact]
    public void AHungryPickerWhoKnowsHowToEatEatsFromTheHarvestFirstAndPocketsTheRest()
    {
        // Twenty apples come off the tree: five eaten (hunger 5, one per apple), fifteen pocketed.
        // The eating threshold has its own tests, so it is put out of the way here.
        var world = CreateWorldWhereAnyHungerIsWorthEating();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 5f;
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(0f, person.Needs.Hunger);
        Assert.Equal(15, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(80f, node.Growth!.RemainingAmount);
    }

    [Fact]
    public void EatingFromTheHarvestTrainsEatingAsWellAsTheGatheringSkill()
    {
        // As above: eating on the spot is under test, not the hunger threshold.
        var world = CreateWorldWhereAnyHungerIsWorthEating();
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
        // Nothing can be pocketed, but a hungry person who knows how to eat still eats on the
        // spot, and only what was eaten comes off the tree.
        var world = CreateWorldWhereAnyHungerIsWorthEating();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 8f;
        FillTheBackpackWithWood(world, person);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);
        var command = new GatherCommand(person, node);

        Assert.Equal(ActionBlocker.None, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(0f, person.Needs.Hunger);
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(92f, node.Growth!.RemainingAmount);
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
        Assert.Equal(80f, node.Growth!.RemainingAmount);
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
        // Grass restores nothing, so it all goes into the backpack and hunger stays where it was.
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 60f;
        var node = world.SpawnResourceNode(TestCatalogs.Grass, new Position(0, 0), 100);

        world.Execute(new GatherCommand(person, node));

        Assert.Equal(60f, person.Needs.Hunger);
        Assert.Equal(20, person.Inventory.Get(TestCatalogs.GrassItem));
        Assert.Equal(80f, node.Growth!.RemainingAmount);
    }

    [Fact]
    public void GatheringIntoAFullBackpackTakesNothingAndEarnsNoPractice()
    {
        // Coming away with nothing is not gathering: the node is untouched and the skill stays
        // put, so a full backpack cannot grind out the efficient technique.
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        FillTheBackpackWithWood(world, person);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);
        var command = new GatherCommand(person, node);

        Assert.Equal(ActionBlocker.InventoryFull, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(100f, node.Growth!.RemainingAmount);
        Assert.Equal(0f, person.Skills.Get(TestCatalogs.Foraging));
    }

    private static void FillTheBackpackWithWood(WorldState world, Person person)
    {
        var woodWeight = world.Configuration.ItemCatalog.WeightFor(TestCatalogs.WoodItem);
        person.Inventory.Add(TestCatalogs.WoodItem, (int)Math.Ceiling(world.MaxCarryWeightFor(person) / woodWeight));
    }

    [Fact]
    public void NothingBlocksGatheringFromAFullNodeWithinReachBySomebodyTaught()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        Assert.Equal(ActionBlocker.None, new GatherCommand(person, node).Blocker(world));
    }

    [Fact]
    public void ADeadPersonIsBlockedFromGathering()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.IsAlive = false;
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        Assert.Equal(ActionBlocker.ActorIsDead, new GatherCommand(person, node).Blocker(world));
    }

    [Fact]
    public void AFelledNodeBlocksTheGatherAsTargetIsGone()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);
        node.Growth!.IsAlive = false;

        Assert.Equal(ActionBlocker.TargetIsGone, new GatherCommand(person, node).Blocker(world));
    }

    // Standing, but picked bare - it will regrow, so this is not the same as TargetIsGone.
    [Fact]
    public void APickedBareNodeBlocksTheGatherAsNothingLeft()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 0);

        Assert.Equal(ActionBlocker.NothingLeft, new GatherCommand(person, node).Blocker(world));
    }

    [Fact]
    public void ANodeOutOfReachBlocksTheGatherAsTooFar()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0), 100);

        Assert.Equal(ActionBlocker.TooFar, new GatherCommand(person, node).Blocker(world));
    }

    [Fact]
    public void NeverHavingBeenTaughtBlocksTheGatherAsNotLearned()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        Assert.Equal(ActionBlocker.NotLearned, new GatherCommand(person, node).Blocker(world));
    }
}
