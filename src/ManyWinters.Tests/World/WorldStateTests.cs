using ManyWinters.Core.Commands;
using ManyWinters.Core.Construction;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

public class WorldStateTests
{
    [Fact]
    public void DistanceBetweenTheSamePositionIsZero()
    {
        Assert.Equal(0f, WorldState.Distance(new Position(3, 4), new Position(3, 4)));
    }

    [Fact]
    public void DistanceMeasuresAlongTheXAxis()
    {
        Assert.Equal(5f, WorldState.Distance(new Position(0, 0), new Position(5, 0)));
    }

    [Fact]
    public void DistanceMeasuresAlongTheYAxis()
    {
        Assert.Equal(5f, WorldState.Distance(new Position(0, 0), new Position(0, 5)));
    }

    [Fact]
    public void DistanceMeasuresDiagonally()
    {
        Assert.Equal(5f, WorldState.Distance(new Position(0, 0), new Position(3, 4)));
    }

    [Fact]
    public void DistanceIsSymmetric()
    {
        var a = new Position(1, 2);
        var b = new Position(4, 6);

        Assert.Equal(WorldState.Distance(a, b), WorldState.Distance(b, a));
    }

    [Fact]
    public void AddPersonTracksThemInPeople()
    {
        var world = TestCatalogs.CreateWorld();
        var ava = new Person { Name = "Ava", BirthTick = 0, Mother = Person.Unknown, Father = Person.Unknown };

        world.AddPerson(ava);

        Assert.Same(ava, Assert.Single(world.People));
    }

    [Fact]
    public void AddForebearTracksThemInForebearsNotPeople()
    {
        var world = TestCatalogs.CreateWorld();
        var forebear = new Person { Name = "Orla", BirthTick = -100, IsAlive = false, Mother = Person.Unknown, Father = Person.Unknown };

        world.AddForebear(forebear);

        Assert.Same(forebear, Assert.Single(world.Forebears));
        Assert.Empty(world.People);
    }

    [Fact]
    public void AddForebearRaisesNoPersonAddedAndExploresNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var raised = false;
        world.PersonAdded += _ => raised = true;

        world.AddForebear(new Person { Name = "Orla", BirthTick = -100, IsAlive = false, Mother = Person.Unknown, Father = Person.Unknown });

        Assert.False(raised);
        Assert.Empty(world.Exploration.Explored);
    }

    [Fact]
    public void AddForebearRejectsALivingPerson()
    {
        var world = TestCatalogs.CreateWorld();
        var alive = new Person { Name = "Orla", BirthTick = 0, Mother = Person.Unknown, Father = Person.Unknown };

        Assert.Throws<ArgumentException>(() => world.AddForebear(alive));

        Assert.Empty(world.Forebears);
    }

    [Fact]
    public void AddPersonRefreshesExplorationAroundThem()
    {
        var world = TestCatalogs.CreateWorld();

        world.AddPerson(new Person { Name = "Ava", BirthTick = 0, Position = new Position(0, 0), Mother = Person.Unknown, Father = Person.Unknown });

        Assert.NotEmpty(world.Exploration.Explored);
    }

    [Fact]
    public void NewWorldHasNoPeopleAndTickZero()
    {
        var world = TestCatalogs.CreateWorld();

        Assert.Empty(world.People);
        Assert.Equal(0, world.Clock.CurrentTick);
    }

    [Fact]
    public void AdvanceMovesTheClockForward()
    {
        var world = TestCatalogs.CreateWorld();

        world.Advance(5);

        Assert.Equal(5, world.Clock.CurrentTick);
    }

    [Fact]
    public void AdvanceIncreasesHungerForEveryPerson()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        world.Advance(3);

        Assert.Equal(3f, person.Needs.Hunger);
        Assert.True(person.IsAlive);
    }

    [Fact]
    public void AdvanceClampsHungerAtItsMaximum()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        world.Advance(1000);

        Assert.Equal(100f, person.Needs.Hunger);
    }

    [Fact]
    public void AdvanceKillsAPersonWhoseHungerReachesTheMaximum()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        world.Advance(99);
        Assert.True(person.IsAlive);

        world.Advance(1);

        Assert.False(person.IsAlive);
    }

    [Fact]
    public void AgeInYearsIsZeroForANewbornPerson()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        Assert.Equal(0, world.AgeInYears(person));
    }

    [Fact]
    public void AgeInYearsIncreasesAfterAFullYearPasses()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        world.Advance(world.Configuration.Rules.TicksPerYear);

        Assert.Equal(1, world.AgeInYears(person));
    }

    [Fact]
    public void AgeInSeasonsIsZeroForANewbornPerson()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        Assert.Equal(0, world.AgeInSeasons(person));
    }

    [Fact]
    public void AgeInSeasonsIncreasesAfterASeasonPasses()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        world.Advance(75);

        Assert.Equal(1, world.AgeInSeasons(person));
    }

    [Fact]
    public void AgeInSeasonsAccountsForThePersonsBirthTickNotJustElapsedWorldTime()
    {
        var world = TestCatalogs.CreateWorld();
        world.Advance(75);
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        world.Advance(75);

        Assert.Equal(1, world.AgeInSeasons(person));
    }

    [Fact]
    public void AgeInYearsAccountsForThePersonsBirthTickNotJustElapsedWorldTime()
    {
        var world = TestCatalogs.CreateWorld();
        world.Advance(world.Configuration.Rules.TicksPerYear);
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        world.Advance(world.Configuration.Rules.TicksPerYear);

        Assert.Equal(1, world.AgeInYears(person));
    }

    [Fact]
    public void MaxCarryWeightForAnAdultWithNoGearIsTheAdultBaseline()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);

        Assert.Equal(CarryCapacity.AdultBaseWeight, world.MaxCarryWeightFor(person));
    }

    [Fact]
    public void MaxCarryWeightForAddsTheBonusOfGearCurrentlyHeld()
    {
        var bag = new ItemKindId("bag");
        var world = new WorldState(new WorldConfiguration
        {
            ItemCatalog = new ItemCatalog(
                new[] { new ItemDefinition(bag, "Bag", new MaterialId("plant_fibre"), new FormId("vessel"), CarryCapacityBonus: 20f) },
                new MaterialCatalog([])),
        });
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.Inventory.Add(bag, 1);

        Assert.Equal(CarryCapacity.AdultBaseWeight + 20f, world.MaxCarryWeightFor(person));
    }

    [Fact]
    public void MaxCarryWeightForGearBonusDoesNotStackWithMoreCopiesOfTheSameItem()
    {
        var bag = new ItemKindId("bag");
        var world = new WorldState(new WorldConfiguration
        {
            ItemCatalog = new ItemCatalog(
                new[] { new ItemDefinition(bag, "Bag", new MaterialId("plant_fibre"), new FormId("vessel"), CarryCapacityBonus: 20f) },
                new MaterialCatalog([])),
        });
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.Inventory.Add(bag, 3);

        Assert.Equal(CarryCapacity.AdultBaseWeight + 20f, world.MaxCarryWeightFor(person));
    }

    [Fact]
    public void AdvanceGivesAPersonWithNoOrdersAnIdleTaskInsteadOfLeavingThemFrozen()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        var start = person.Position;

        world.Advance(20);

        Assert.IsType<IdleTask>(person.Tasks.Current);
        Assert.NotEqual(start, person.Position);
    }

    [Fact]
    public void AdvanceDoesNotReplaceAnAlreadyInProgressTaskWithIdle()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        world.Execute(new MoveCommand(person, new Position(100, 0)));

        world.Advance(1);

        Assert.IsType<MoveTask>(person.Tasks.Current);
    }

    [Fact]
    public void AdvanceDoesNotStartIdleWanderingWhileAGracePeriodIsStillRunning()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        world.Execute(new GrantIdleGraceCommand(person, 5));
        var start = person.Position;

        world.Advance(4);

        Assert.Null(person.Tasks.Current);
        Assert.Equal(start, person.Position);
    }

    [Fact]
    public void AdvanceStartsIdleWanderingAsSoonAsTheGracePeriodRunsOut()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        world.Execute(new GrantIdleGraceCommand(person, 5));

        world.Advance(5);

        Assert.IsType<IdleTask>(person.Tasks.Current);
    }

    [Fact]
    public void AdvanceHasAnIdlePersonWithAKnownSkillAutonomouslyGatherFromAMatchingResource()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);
        var node = world.SpawnResourceNode(TestCatalogs.Wood, new Position(0, 0), 100f);

        world.Advance(1);

        var task = Assert.IsType<GatherTask>(person.Tasks.Current);
        Assert.Equal(node.Id, task.Target.Id);
        Assert.True(person.Inventory.Get(TestCatalogs.WoodItem) > 0);
    }

    [Fact]
    public void AdvanceHasAnIdlePersonWalkTowardAKnownSkillsResourceBeforeGatheringOnceInRange()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);
        world.SpawnResourceNode(TestCatalogs.Wood, new Position(10, 0), 100f);

        // First tick only assigns the GatherTask (same one-tick lag as IdleTask itself - see
        // AdvanceGivesAPersonWithNoOrdersAnIdleTaskInsteadOfLeavingThemFrozen); it needs a
        // second tick to actually advance the walk.
        world.Advance(2);

        Assert.IsType<GatherTask>(person.Tasks.Current);
        Assert.True(person.Position.X > 0);
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void AdvanceHasAHungryPersonWithNoFoodAutonomouslySeekTheNearestFoodResourceWhenIdle()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.Needs.Hunger = 60f;
        var foodNode = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100f);

        world.Advance(1);

        var task = Assert.IsType<GatherTask>(person.Tasks.Current);
        Assert.Equal(foodNode.Id, task.Target.Id);
    }

    [Fact]
    public void AdvanceHasSeekingFoodWhenHungryTakePriorityOverAnAlreadyKnownSkill()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.Needs.Hunger = 60f;
        world.SpawnResourceNode(TestCatalogs.Wood, new Position(0, 0), 100f);
        var foodNode = world.SpawnResourceNode(TestCatalogs.Apple, new Position(5, 0), 100f);

        world.Advance(1);

        var task = Assert.IsType<GatherTask>(person.Tasks.Current);
        Assert.Equal(foodNode.Id, task.Target.Id);
    }

    [Fact]
    public void AdvanceEatsFromInventoryWhenHungryEvenWhileOnAPlayerIssuedTask()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 30f;
        person.Inventory.Add(TestCatalogs.AppleItem, 50);
        world.Execute(new MoveCommand(person, new Position(100, 0)));

        world.Advance(1);

        Assert.IsType<MoveTask>(person.Tasks.Current);
        Assert.Equal(0f, person.Needs.Hunger);
        Assert.True(person.Inventory.Get(TestCatalogs.AppleItem) < 50);
    }

    [Fact]
    public void AdvanceDoesNotSendAnIdlePersonBeyondIdleSearchRadiusForAKnownSkillsResource()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);
        world.SpawnResourceNode(TestCatalogs.Wood, new Position(1000, 0), 100f);

        world.Advance(1);

        Assert.IsType<IdleTask>(person.Tasks.Current);
    }

    [Fact]
    public void AdvanceReassignsAGatherTaskOnceItsTargetResourceStopsBeingWorthGathering()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);
        var primary = world.SpawnResourceNode(TestCatalogs.Wood, new Position(0, 0), 100f);
        var backup = world.SpawnResourceNode(TestCatalogs.Wood, new Position(10, 0), 100f);

        world.Advance(1);
        Assert.Equal(primary.Id, ((GatherTask)person.Tasks.Current!).Target.Id);

        primary.IsAlive = false;
        world.Advance(1);

        var task = Assert.IsType<GatherTask>(person.Tasks.Current);
        Assert.Equal(backup.Id, task.Target.Id);
    }

    [Fact]
    public void AdvanceRedirectsAGatherTaskToSeekFoodOnceHungerBecomesUrgentPartwayThroughAFarErrand()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        // Far enough that Ava is still walking there, not yet gathering, when hunger hits.
        var farWood = world.SpawnResourceNode(TestCatalogs.Wood, new Position(50, 0), 100f);

        world.Advance(1);
        Assert.Equal(farWood.Id, ((GatherTask)person.Tasks.Current!).Target.Id);

        // A closer food source only becomes relevant once hunger turns urgent - otherwise
        // she'd have gone for it from the very start instead of the (nearer, at the time) wood.
        var nearbyFood = world.SpawnResourceNode(TestCatalogs.Apple, new Position(1, 0), 100f);
        person.Needs.Hunger = 90f;
        world.Advance(1);

        var task = Assert.IsType<GatherTask>(person.Tasks.Current);
        Assert.Equal(nearbyFood.Id, task.Target.Id);
    }

    [Fact]
    public void AdvanceReconsidersAnIdlePersonOnceSomethingWorthGatheringTurnsUp()
    {
        // Idle is the one autonomous choice that always gets a second look - something better
        // might now apply that didn't when they started wandering.
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);

        world.Advance(1);
        Assert.IsType<IdleTask>(person.Tasks.Current);

        var node = world.SpawnResourceNode(TestCatalogs.Wood, new Position(3, 0), 100f);
        world.Advance(1);

        var task = Assert.IsType<GatherTask>(person.Tasks.Current);
        Assert.Equal(node.Id, task.Target.Id);
    }

    [Theory]
    [InlineData(49f, false)]
    [InlineData(50f, true)]
    public void AdvanceSendsAPersonLookingForFoodTheMomentHungerReachesTheThresholdNotOnlyPastIt(float hunger, bool expectFood)
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);
        person.Needs.Hunger = hunger;

        // Wood is much the nearer of the two, so it wins on distance alone right up until
        // hunger turns urgent and food starts being sought ahead of everything else.
        var wood = world.SpawnResourceNode(TestCatalogs.Wood, new Position(1, 0), 100f);
        var food = world.SpawnResourceNode(TestCatalogs.Apple, new Position(30, 0), 100f);

        world.Advance(1);

        var task = Assert.IsType<GatherTask>(person.Tasks.Current);
        Assert.Equal(expectFood ? food.Id : wood.Id, task.Target.Id);
    }

    [Fact]
    public void AdvanceLeavesAHungryPersonWhoIsCarryingFoodToGetOnWithWhateverElseTheyKnow()
    {
        // Hunger only redirects someone who has nothing to eat on them - a woodcutter with
        // apples in their pack has no reason to break off and go pick more.
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);
        person.Needs.Hunger = 60f;
        person.Inventory.Add(TestCatalogs.AppleItem, 5);

        var wood = world.SpawnResourceNode(TestCatalogs.Wood, new Position(1, 0), 100f);
        world.SpawnResourceNode(TestCatalogs.Apple, new Position(30, 0), 100f);

        world.Advance(1);

        var task = Assert.IsType<GatherTask>(person.Tasks.Current);
        Assert.Equal(wood.Id, task.Target.Id);
    }

    [Fact]
    public void AdvanceStillSendsAHungryPersonForFoodWhenAllTheyAreCarryingIsInedible()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);
        person.Needs.Hunger = 60f;
        person.Inventory.Add(TestCatalogs.WoodItem, 5);

        world.SpawnResourceNode(TestCatalogs.Wood, new Position(1, 0), 100f);
        var food = world.SpawnResourceNode(TestCatalogs.Apple, new Position(30, 0), 100f);

        world.Advance(1);

        var task = Assert.IsType<GatherTask>(person.Tasks.Current);
        Assert.Equal(food.Id, task.Target.Id);
    }

    [Fact]
    public void AdvanceDoesNotCountAnEmptyInventoryEntryAsFoodOnHand()
    {
        // A kind whose count has fallen to zero is still a key in the inventory; counting it
        // would leave a starving person convinced they have an apple left.
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);
        person.Needs.Hunger = 60f;
        person.Inventory.Add(TestCatalogs.AppleItem, 0);

        world.SpawnResourceNode(TestCatalogs.Wood, new Position(1, 0), 100f);
        var food = world.SpawnResourceNode(TestCatalogs.Apple, new Position(30, 0), 100f);

        world.Advance(1);

        var task = Assert.IsType<GatherTask>(person.Tasks.Current);
        Assert.Equal(food.Id, task.Target.Id);
    }

    [Fact]
    public void AdvanceReassignsAGatherTaskOnceItsTargetHasBeenPickedCleanEvenThoughItIsStillAlive()
    {
        // Depleted-but-alive nodes regenerate eventually, but standing next to one waiting is
        // not the plan - with a fuller one of the same kind nearby, that's where to go.
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);
        var primary = world.SpawnResourceNode(TestCatalogs.Wood, new Position(0, 0), 100f);
        var backup = world.SpawnResourceNode(TestCatalogs.Wood, new Position(10, 0), 100f);

        world.Advance(1);
        Assert.Equal(primary.Id, ((GatherTask)person.Tasks.Current!).Target.Id);

        primary.RemainingAmount = 0f;
        world.Advance(1);

        var task = Assert.IsType<GatherTask>(person.Tasks.Current);
        Assert.Equal(backup.Id, task.Target.Id);
    }

    [Fact]
    public void AdvanceLeavesAPersonWanderingWhenTheOnlyResourceAroundNeedsASkillTheCatalogNeverHeardOf()
    {
        // Find, not Get, all the way down: a resource pointing at a skill nobody registered
        // just means nobody can work it, not a crash mid-tick.
        var unknownSkillResource = new ResourceKindId("moon_rock");
        var configuration = TestCatalogs.CreateConfiguration() with
        {
            ResourceCatalog = new ResourceCatalog([new ResourceDefinition(unknownSkillResource, "Moon Rock", new SkillTypeId("moon_mining"))]),
        };
        var world = new WorldState(configuration);
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicMining);
        world.SpawnResourceNode(unknownSkillResource, new Position(1, 0), 100f);

        world.Advance(1);

        Assert.IsType<IdleTask>(person.Tasks.Current);
    }

    [Fact]
    public void AdvanceSendsAnIdlePersonToTheFirstOfTwoEquallyDistantResources()
    {
        // Nothing about a tie makes the later node the better pick, and picking one of them
        // consistently is what keeps a world reproducible from the same starting state.
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);
        var first = world.SpawnResourceNode(TestCatalogs.Wood, new Position(5, 0), 100f);
        world.SpawnResourceNode(TestCatalogs.Wood, new Position(-5, 0), 100f);

        world.Advance(1);

        var task = Assert.IsType<GatherTask>(person.Tasks.Current);
        Assert.Equal(first.Id, task.Target.Id);
    }

    [Fact]
    public void AdvanceStillSendsAnIdlePersonToAResourceSittingExactlyAtTheIdleSearchRadius()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);
        var node = world.SpawnResourceNode(TestCatalogs.Wood, new Position(60, 0), 100f);

        world.Advance(1);

        var task = Assert.IsType<GatherTask>(person.Tasks.Current);
        Assert.Equal(node.Id, task.Target.Id);
    }

    [Fact]
    public void AdvanceLeavesADeadResourceNodeAloneInsteadOfRegrowingIt()
    {
        var world = TestCatalogs.CreateWorld();
        var node = world.SpawnResourceNode(TestCatalogs.Grass, new Position(0, 0), 100f);
        node.RemainingAmount = 10f;
        node.IsAlive = false;

        world.Advance(20);

        Assert.Equal(10f, node.RemainingAmount);
        Assert.Equal(0f, node.ColdStress);
    }

    [Fact]
    public void AdvancePushesTwoOverlappingPeopleApart()
    {
        var world = TestCatalogs.CreateWorld();
        var a = world.SpawnPerson("Ava", new Position(0, 0));
        var b = world.SpawnPerson("Bran", new Position(0, 0));

        world.Advance(1);

        Assert.True(WorldState.Distance(a.Position, b.Position) > 0.5);
    }

    [Fact]
    public void AdvanceLeavesTwoAlreadyFarApartPeopleExactlyWhereTheyWere()
    {
        var world = TestCatalogs.CreateWorld();
        var a = world.SpawnPerson("Ava", new Position(0, 0));
        var b = world.SpawnPerson("Bran", new Position(10, 0));

        world.Advance(1);

        Assert.Equal(new Position(0, 0), a.Position);
        Assert.Equal(new Position(10, 0), b.Position);
    }

    [Fact]
    public void AdvanceDoesNotPushADeadPersonAwayFromALivingOne()
    {
        var world = TestCatalogs.CreateWorld();
        var dead = world.SpawnPerson("Ava", new Position(0, 0));
        dead.IsAlive = false;
        world.SpawnPerson("Bran", new Position(0, 0));

        world.Advance(1);

        Assert.Equal(new Position(0, 0), dead.Position);
    }

    [Fact]
    public void AdvancePushesAPersonOutOfATreesTrunk()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        world.SpawnResourceNode(TestCatalogs.ConiferTree, new Position(0, 0), 100f);

        world.Advance(1);

        Assert.True(WorldState.Distance(person.Position, new Position(0, 0)) > 0.5);
    }

    [Fact]
    public void AdvanceDoesNotPushAPersonAwayFromAWalkThroughResourceLikeGrass()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        world.SpawnResourceNode(TestCatalogs.Grass, new Position(0, 0), 100f);

        world.Advance(1);

        Assert.Equal(new Position(0, 0), person.Position);
    }

    [Fact]
    public void AdvanceNeverPushesAPersonFartherThanOneTicksWorthOfWalkingEvenWhenSurroundedByManyTrees()
    {
        // A person standing in a dense thicket could be overlapping several trees' trunks at
        // once. Summing every one of those separations unclamped would shove them noticeably
        // farther in a single tick than their own walk speed - reading as their move order
        // having been hijacked toward some unrelated direction rather than a gentle nudge.
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        for (var i = 0; i < 8; i++)
        {
            world.SpawnResourceNode(TestCatalogs.ConiferTree, new Position(0, 0), 100f);
        }

        world.Advance(1);

        Assert.True(WorldState.Distance(person.Position, new Position(0, 0)) <= 1.0);
    }

    [Fact]
    public void AdvancePushesAPersonFartherOutOfABiggerRockThanASmallerOne()
    {
        // A rock's real-world footprint - not just whether it happens to be fellable - is
        // what decides how solid it is; a boulder should shove someone out farther than a
        // loose pile of rocks with the same starting overlap.
        var pileWorld = TestCatalogs.CreateWorld();
        var personNearPile = pileWorld.SpawnPerson("Ava", new Position(0, 0));
        pileWorld.SpawnResourceNode(TestCatalogs.RockPile, new Position(0, 0), 100f);

        var boulderWorld = TestCatalogs.CreateWorld();
        var personNearBoulder = boulderWorld.SpawnPerson("Ava", new Position(0, 0));
        boulderWorld.SpawnResourceNode(TestCatalogs.RockBoulder, new Position(0, 0), 100f);

        pileWorld.Advance(1);
        boulderWorld.Advance(1);

        var pushedByPile = WorldState.Distance(personNearPile.Position, new Position(0, 0));
        var pushedByBoulder = WorldState.Distance(personNearBoulder.Position, new Position(0, 0));
        Assert.True(pushedByBoulder > pushedByPile);
    }

    [Theory]
    [InlineData(0, 4, 1)]
    [InlineData(2, 1, 3)]
    public void AutoTeachNearbyPeopleRollsTheSameWayEveryRunForAGivenPairAndTick(
        int peopleBefore, int expectedTeachingTick, int expectedWoodcuttingTick)
    {
        // The roll is derived from the teacher's and student's id seeds, the technique and the
        // tick alone - no shared mutable Random, so the same starting state always plays out
        // the same way regardless of the order people happen to be advanced in. Pinning the
        // exact ticks is what actually holds that: a hash that quietly changed would still look
        // random, just not the same random. The seeds are chosen (see TestIds) - a randomly
        // drawn id would make the pinned ticks meaningless.
        var world = TestCatalogs.CreateWorld();
        for (var i = 0; i < peopleBefore; i++)
        {
            world.SpawnPerson($"Bystander {i}", new Position(500, 500));
        }

        var teacher = world.SpawnPerson(TestIds.Person(peopleBefore + 1), "Ava", new Position(0, 0));
        var student = world.SpawnPerson(TestIds.Person(peopleBefore + 2), "Bran", new Position(1, 0));
        teacher.KnownTechniques.Add(TestCatalogs.BasicTeaching);
        teacher.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);

        var learnedTeaching = -1;
        var learnedWoodcutting = -1;
        for (var tick = 1; tick <= 50; tick++)
        {
            foreach (var person in world.People)
            {
                person.Needs.Hunger = 0f;
            }

            world.Advance(1);
            if (learnedTeaching < 0 && student.KnownTechniques.Contains(TestCatalogs.BasicTeaching))
            {
                learnedTeaching = tick;
            }

            if (learnedWoodcutting < 0 && student.KnownTechniques.Contains(TestCatalogs.BasicWoodcutting))
            {
                learnedWoodcutting = tick;
            }
        }

        Assert.Equal(expectedTeachingTick, learnedTeaching);
        Assert.Equal(expectedWoodcuttingTick, learnedWoodcutting);
    }

    [Fact]
    public void AutoTeachNearbyPeopleSpreadsEatingFasterThanASpecialisedSkill()
    {
        var world = TestCatalogs.CreateWorld();
        var teacher = world.SpawnPerson("Teacher", new Position(0, 0));
        teacher.KnownTechniques.Add(TestCatalogs.BasicTeaching);
        teacher.KnownTechniques.Add(TestCatalogs.BasicEating);
        teacher.KnownTechniques.Add(TestCatalogs.BasicForaging);

        var students = new List<Person>();
        for (var i = 0; i < 30; i++)
        {
            students.Add(world.SpawnPerson($"Student{i}", new Position(0, 0)));
        }

        world.Advance(1);

        var learnedEating = students.Count(s => s.KnownTechniques.Contains(TestCatalogs.BasicEating));
        var learnedForaging = students.Count(s => s.KnownTechniques.Contains(TestCatalogs.BasicForaging));
        Assert.True(learnedEating > learnedForaging, $"Expected eating ({learnedEating}) to spread faster than foraging ({learnedForaging}) in the same tick.");
    }

    [Fact]
    public void AutoTeachNearbyPeopleSpreadsGraduallyNotInstantlyToEveryNearbyStudentAtOnce()
    {
        var world = TestCatalogs.CreateWorld();
        var teacher = world.SpawnPerson("Teacher", new Position(0, 0));
        teacher.KnownTechniques.Add(TestCatalogs.BasicTeaching);
        teacher.KnownTechniques.Add(TestCatalogs.BasicForaging);

        var students = new List<Person>();
        for (var i = 0; i < 20; i++)
        {
            students.Add(world.SpawnPerson($"Student{i}", new Position(0, 0)));
        }

        world.Advance(1);

        var learnedCount = students.Count(s => s.KnownTechniques.Contains(TestCatalogs.BasicForaging));
        Assert.True(learnedCount < students.Count, "Expected casual teaching to spread gradually - not every nearby student should pick it up in a single tick.");
    }

    [Fact]
    public void AutoTeachNearbyPeopleNeverSpreadsTheEfficientTechniqueOnlyTheBaseOne()
    {
        var world = TestCatalogs.CreateWorld();
        var teacher = world.SpawnPerson("Teacher", new Position(0, 0));
        teacher.KnownTechniques.Add(TestCatalogs.BasicTeaching);
        teacher.KnownTechniques.Add(TestCatalogs.BasicForaging);
        teacher.KnownTechniques.Add(TestCatalogs.EfficientForaging);

        var students = new List<Person>();
        for (var i = 0; i < 20; i++)
        {
            students.Add(world.SpawnPerson($"Student{i}", new Position(0, 0)));
        }

        world.Advance(20);

        Assert.All(students, s => Assert.DoesNotContain(TestCatalogs.EfficientForaging, s.KnownTechniques));
    }

    [Fact]
    public void AutoTeachNearbyPeopleSpreadsToMoreStudentsGivenMoreTime()
    {
        var world = TestCatalogs.CreateWorld();
        var teacher = world.SpawnPerson("Teacher", new Position(0, 0));
        teacher.KnownTechniques.Add(TestCatalogs.BasicTeaching);
        teacher.KnownTechniques.Add(TestCatalogs.BasicForaging);

        var students = new List<Person>();
        for (var i = 0; i < 20; i++)
        {
            students.Add(world.SpawnPerson($"Student{i}", new Position(0, 0)));
        }

        world.Advance(1);
        var learnedSoon = students.Count(s => s.KnownTechniques.Contains(TestCatalogs.BasicForaging));

        world.Advance(4);
        var learnedLater = students.Count(s => s.KnownTechniques.Contains(TestCatalogs.BasicForaging));

        Assert.True(learnedLater > learnedSoon, "Expected more students to have picked up the technique after more time near the teacher.");
    }

    [Fact]
    public void AdvanceAssignsTheExactCurrentTickWhenDeathOccursMidwayThroughAMultiTickAdvance()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Needs.Hunger = 98;

        world.Advance(3);

        Assert.Equal(2, person.DeathTick);
    }

    [Fact]
    public void AdvanceDoesNotKeepUpdatingDeathTickForAnAlreadyDeadPerson()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        world.Advance(100);
        Assert.Equal(100, person.DeathTick);

        world.Advance(10);

        Assert.Equal(100, person.DeathTick);
    }

    [Fact]
    public void AdvanceSubtractsBirthTickRatherThanAddingItWhenCheckingOldAgeDeath()
    {
        var world = TestCatalogs.CreateWorld();
        world.Clock.Advance(2900);
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        world.Advance(1);

        Assert.True(person.IsAlive);
    }

    // A short life on a short calendar (SimulationRules) - old age arrives after a handful of
    // ticks instead of the shipped 3000, so these tests don't have to simulate a whole decade.
    private static readonly SimulationRules ShortLifeRules = new() { TicksPerSeason = 2, MaxLifespanYears = 3 };

    private static WorldState CreateWorld(SimulationRules rules) =>
        new(TestCatalogs.CreateConfiguration() with { Rules = rules });

    [Fact]
    public void AdvanceKillsAPersonWhoReachesTheMaximumLifespanEvenWhenNeverHungry()
    {
        var world = CreateWorld(ShortLifeRules);
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        var lifespanTicks = ShortLifeRules.TicksPerYear * ShortLifeRules.MaxLifespanYears;

        // Feed the person back to zero after every tick so only old age - never hunger - can
        // be responsible for their death.
        for (var tick = 0; tick < lifespanTicks - 1; tick++)
        {
            world.Advance(1);
            person.Needs.Hunger = 0;
        }

        Assert.True(person.IsAlive);

        world.Advance(1);

        Assert.False(person.IsAlive);
        Assert.Equal(lifespanTicks, person.DeathTick);
    }

    [Fact]
    public void AdvanceUsesTheShippedLifespanWhenNoRulesAreOverridden()
    {
        // Pins the default calendar: 4 seasons of 75 ticks, 10 years - a person born at tick 0
        // is still alive on tick 2999 and dead on tick 3000.
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        world.Clock.Advance(2999);
        world.Advance(1);

        Assert.False(person.IsAlive);
        Assert.Equal(3000, person.DeathTick);
        Assert.Equal(DeathCause.OldAge, person.CauseOfDeath);
    }

    [Fact]
    public void AdvanceRecordsHungerAsTheCauseOfDeathWhenHungerReachesMaximum()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        world.Advance(100);

        Assert.Equal(DeathCause.Hunger, person.CauseOfDeath);
    }

    [Fact]
    public void AdvanceRecordsOldAgeAsTheCauseOfDeathWhenTheMaximumLifespanIsReached()
    {
        var world = CreateWorld(ShortLifeRules);
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        for (var tick = 0; tick < (ShortLifeRules.TicksPerYear * ShortLifeRules.MaxLifespanYears) - 1; tick++)
        {
            world.Advance(1);
            person.Needs.Hunger = 0;
        }

        world.Advance(1);

        Assert.Equal(DeathCause.OldAge, person.CauseOfDeath);
    }

    [Fact]
    public void AdvancePrioritizesOldAgeAsTheCauseOfDeathWhenBothConditionsAreMetSimultaneously()
    {
        var world = CreateWorld(ShortLifeRules);
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        for (var tick = 0; tick < (ShortLifeRules.TicksPerYear * ShortLifeRules.MaxLifespanYears) - 1; tick++)
        {
            world.Advance(1);
            person.Needs.Hunger = 0;
        }

        person.Needs.Hunger = ShortLifeRules.MaxHunger - 1;
        world.Advance(1);

        Assert.Equal(DeathCause.OldAge, person.CauseOfDeath);
    }

    [Fact]
    public void AdvanceRaisesHungerByTheConfiguredAmountPerTickAndCapsItAtTheConfiguredMaximum()
    {
        var world = CreateWorld(new SimulationRules { HungerPerTick = 30f, MaxHunger = 70f });
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        world.Advance(2);
        Assert.Equal(60f, person.Needs.Hunger);
        Assert.True(person.IsAlive);

        world.Advance(1);
        Assert.Equal(70f, person.Needs.Hunger);
        Assert.False(person.IsAlive);
        Assert.Equal(DeathCause.Hunger, person.CauseOfDeath);
    }

    [Fact]
    public void AdvanceDecaysBuildingConditionByTheConfiguredRate()
    {
        var world = CreateWorld(new SimulationRules { ConditionDecayPerTick = 10f });
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));

        world.Advance(3);

        Assert.Equal(70f, building.Condition);
    }

    [Fact]
    public void CurrentSeasonFollowsTheConfiguredSeasonLength()
    {
        var world = CreateWorld(new SimulationRules { TicksPerSeason = 2 });

        world.Advance(2);
        Assert.Equal(Season.Summer, world.CurrentSeason);

        world.Advance(6);
        Assert.Equal(Season.Spring, world.CurrentSeason);
    }

    [Fact]
    public void IsWithinReachAcceptsExactlyTheConfiguredDistanceAndRejectsAnythingFurther()
    {
        var world = CreateWorld(new SimulationRules { MaxInteractionDistance = 3f });

        Assert.True(world.IsWithinReach(new Position(0, 0), new Position(3, 0)));
        Assert.False(world.IsWithinReach(new Position(0, 0), new Position(3.01, 0)));
    }

    [Fact]
    public void IsWithinReachScalesByTheGivenMultiplier()
    {
        var world = CreateWorld(new SimulationRules { MaxInteractionDistance = 3f });

        Assert.True(world.IsWithinReach(new Position(0, 0), new Position(6, 0), rangeMultiplier: 2f));
        Assert.False(world.IsWithinReach(new Position(0, 0), new Position(6.01, 0), rangeMultiplier: 2f));
    }

    [Fact]
    public void AgeInYearsAtMeasuresAgainstTheGivenTickOnTheConfiguredCalendar()
    {
        var world = CreateWorld(new SimulationRules { TicksPerSeason = 5 });
        world.Clock.Advance(20);
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        Assert.Equal(0, world.AgeInYearsAt(person, 39));
        Assert.Equal(1, world.AgeInYearsAt(person, 40));
        Assert.Equal(3, world.AgeInYearsAt(person, 85));
    }

    [Fact]
    public void AutonomousGatherTasksCarryTheWorldsReachDistance()
    {
        var world = CreateWorld(new SimulationRules { MaxInteractionDistance = 0.75f });
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        world.SpawnResourceNode(TestCatalogs.Apple, new Position(10, 0), 100);

        world.Advance(1);

        var gather = Assert.IsType<GatherTask>(person.Tasks.Current);
        Assert.Equal(0.75f, gather.ReachDistance);
    }

    [Fact]
    public void AdvanceLeavesADeceasedPersonsInventoryUntouchedRatherThanTransferringItAutomatically()
    {
        var world = TestCatalogs.CreateWorld();
        var parent = world.SpawnPerson("Ava", new Position(0, 0));
        parent.Needs.Hunger = 99;
        parent.Inventory.Add(TestCatalogs.WoodItem, 5);
        var child = world.SpawnPerson("Bran", new Position(0, 0), mother: parent);

        world.Advance(1);

        Assert.False(parent.IsAlive);
        Assert.Equal(5, parent.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(0, child.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void AddGraveTracksItInGraves()
    {
        var world = TestCatalogs.CreateWorld();
        var grave = new Grave { Position = new Position(2, 3), IsMarked = true, Name = "Ava" };

        world.AddGrave(grave);

        Assert.Same(grave, Assert.Single(world.Graves));
    }

    [Fact]
    public void AddGraveRaisesGraveAddedWithTheNewGrave()
    {
        var world = TestCatalogs.CreateWorld();
        Grave? raised = null;
        world.GraveAdded += g => raised = g;
        var grave = new Grave { Position = new Position(0, 0), IsMarked = false };

        world.AddGrave(grave);

        Assert.Same(grave, raised);
    }

    [Fact]
    public void AddGraveDoesNotThrowWhenNothingIsSubscribedToGraveAdded()
    {
        var world = TestCatalogs.CreateWorld();

        world.AddGrave(new Grave { Position = new Position(0, 0), IsMarked = false });
    }

    [Fact]
    public void AdvanceMovesAPersonWithAnActiveMoveTaskTowardTheirDestination()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        world.Execute(new MoveCommand(person, new Position(10, 0)));

        world.Advance(3);

        Assert.Equal(new Position(3, 0), person.Position);
    }

    [Fact]
    public void AdvanceStopsMovingAPersonOnceTheyReachTheirDestination()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        world.Execute(new MoveCommand(person, new Position(2, 0)));

        world.Advance(2);

        Assert.Equal(new Position(2, 0), person.Position);
    }

    [Fact]
    public void AdvanceLetsAPersonIdleWanderOnceTheyReachTheirDestinationInsteadOfFreezingThere()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        world.Execute(new MoveCommand(person, new Position(2, 0)));

        world.Advance(20);

        Assert.IsType<IdleTask>(person.Tasks.Current);
        Assert.NotEqual(new Position(2, 0), person.Position);
    }

    [Fact]
    public void AdvanceStopsMovingAPersonOnceTheyDieFromHunger()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        world.Execute(new MoveCommand(person, new Position(1000, 0)));

        world.Advance(100);
        Assert.False(person.IsAlive);
        var positionAtDeath = person.Position;

        world.Advance(10);

        Assert.Equal(positionAtDeath, person.Position);
    }

    [Fact]
    public void AddResourceNodeTracksItInResourceNodes()
    {
        var world = TestCatalogs.CreateWorld();
        var node = new ResourceNode { Kind = TestCatalogs.Apple };

        world.AddResourceNode(node);

        Assert.Same(node, Assert.Single(world.ResourceNodes));
    }

    [Fact]
    public void AddPersonRaisesPersonAddedWithTheNewPerson()
    {
        var world = TestCatalogs.CreateWorld();
        Person? raised = null;
        world.PersonAdded += p => raised = p;
        var person = new Person { Name = "Ava", BirthTick = 0, Mother = Person.Unknown, Father = Person.Unknown };

        world.AddPerson(person);

        Assert.Same(person, raised);
    }

    [Fact]
    public void AddPersonDoesNotThrowWhenNothingIsSubscribedToPersonAdded()
    {
        var world = TestCatalogs.CreateWorld();

        world.AddPerson(new Person { Name = "Ava", BirthTick = 0, Mother = Person.Unknown, Father = Person.Unknown });
    }

    [Fact]
    public void AddResourceNodeRaisesResourceNodeAddedWithTheNewNode()
    {
        var world = TestCatalogs.CreateWorld();
        ResourceNode? raised = null;
        world.ResourceNodeAdded += n => raised = n;
        var node = new ResourceNode { Kind = TestCatalogs.Apple };

        world.AddResourceNode(node);

        Assert.Same(node, raised);
    }

    [Fact]
    public void AddResourceNodeDoesNotThrowWhenNothingIsSubscribedToResourceNodeAdded()
    {
        var world = TestCatalogs.CreateWorld();

        world.AddResourceNode(new ResourceNode { Kind = TestCatalogs.Apple });
    }

    [Fact]
    public void AddBuildingTracksItInBuildings()
    {
        var world = TestCatalogs.CreateWorld();
        var building = new Building { Kind = TestCatalogs.StorageHut };

        world.AddBuilding(building);

        Assert.Same(building, Assert.Single(world.Buildings));
    }

    [Fact]
    public void AddBuildingRaisesBuildingAddedWithTheNewBuilding()
    {
        var world = TestCatalogs.CreateWorld();
        Building? raised = null;
        world.BuildingAdded += b => raised = b;
        var building = new Building { Kind = TestCatalogs.StorageHut };

        world.AddBuilding(building);

        Assert.Same(building, raised);
    }

    [Fact]
    public void AddBuildingDoesNotThrowWhenNothingIsSubscribedToBuildingAdded()
    {
        var world = TestCatalogs.CreateWorld();

        world.AddBuilding(new Building { Kind = TestCatalogs.StorageHut });
    }

    [Fact]
    public void AdvanceDecaysBuildingCondition()
    {
        var world = TestCatalogs.CreateWorld();
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));

        world.Advance(100);

        Assert.Equal(95f, building.Condition);
    }

    [Fact]
    public void AdvanceNeverDecaysBuildingConditionBelowZero()
    {
        var world = TestCatalogs.CreateWorld();
        var building = world.SpawnBuilding(TestCatalogs.StorageHut, new Position(0, 0));

        world.Advance(1_000_000);

        Assert.Equal(0f, building.Condition);
    }

    [Fact]
    public void NewWorldStartsInSpring()
    {
        var world = TestCatalogs.CreateWorld();

        Assert.Equal(Season.Spring, world.CurrentSeason);
    }

    [Theory]
    [InlineData(0, Season.Spring)]
    [InlineData(74, Season.Spring)]
    [InlineData(75, Season.Summer)]
    [InlineData(149, Season.Summer)]
    [InlineData(150, Season.Autumn)]
    [InlineData(224, Season.Autumn)]
    [InlineData(225, Season.Winter)]
    [InlineData(299, Season.Winter)]
    [InlineData(300, Season.Spring)]
    public void SeasonChangesAtEachSeasonBoundary(long tick, Season expected)
    {
        var world = TestCatalogs.CreateWorld();

        world.Advance(tick);

        Assert.Equal(expected, world.CurrentSeason);
    }

    [Fact]
    public void AdvanceAppliesDoubleHungerRateDuringWinter()
    {
        var world = TestCatalogs.CreateWorld();
        world.Advance(225);
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        world.Advance(1);

        Assert.Equal(Season.Winter, world.CurrentSeason);
        Assert.Equal(2f, person.Needs.Hunger);
    }

    [Fact]
    public void AdvanceAcrossTheWinterBoundaryAppliesEachTicksOwnRate()
    {
        var world = TestCatalogs.CreateWorld();
        world.Advance(224);
        var person = world.SpawnPerson("Ava", new Position(0, 0));

        // Ticks 224 (Autumn) and 225 (Winter): 1 + 2 = 3 hunger, not a flat rate for both.
        world.Advance(2);

        Assert.Equal(3f, person.Needs.Hunger);
    }

    [Fact]
    public void AdvanceRegrowsADepletedResourceNodeTowardItsMaxAmount()
    {
        var world = TestCatalogs.CreateWorld();
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 200);
        node.RemainingAmount = 50;

        world.Advance(10);

        Assert.Equal(50f + (TestCatalogs.FoodRegenPerTick * 10), node.RemainingAmount);
    }

    [Fact]
    public void AdvanceNeverRegrowsAResourceNodeAboveItsMaxAmount()
    {
        var world = TestCatalogs.CreateWorld();
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 200);
        node.RemainingAmount = 199;

        world.Advance(10);

        Assert.Equal(200f, node.RemainingAmount);
    }

    [Fact]
    public void AdvanceDoesNotRegrowResourceNodesDuringWinter()
    {
        var world = TestCatalogs.CreateWorld();
        world.Advance(225);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 200);
        node.RemainingAmount = 50;

        world.Advance(10);

        Assert.Equal(Season.Winter, world.CurrentSeason);
        Assert.Equal(50f, node.RemainingAmount);
    }

    [Fact]
    public void AdvanceAccumulatesColdStressForANodeWithNoYieldInTheCurrentClimateButKeepsItAliveUnderTheThreshold()
    {
        var kind = new ResourceKindId("frost_intolerant");
        var definition = new ResourceDefinition(
            kind,
            "Frost-Intolerant Plant",
            new SkillTypeId("test"),
            ClimateYields: [new ClimateYield(Climate.Cold, 0f)],
            TicksToWither: 3f);
        var world = new WorldState(new WorldConfiguration { ResourceCatalog = new ResourceCatalog([definition]) });
        world.Advance(225);
        var node = world.SpawnResourceNode(kind, new Position(0, 0), 100);

        world.Advance(2);

        Assert.True(node.IsAlive);
        Assert.Equal(2f, node.ColdStress);
    }

    [Fact]
    public void AdvanceKillsANodeOnceColdStressReachesTicksToWither()
    {
        var kind = new ResourceKindId("frost_intolerant");
        var definition = new ResourceDefinition(
            kind,
            "Frost-Intolerant Plant",
            new SkillTypeId("test"),
            ClimateYields: [new ClimateYield(Climate.Cold, 0f)],
            TicksToWither: 3f);
        var world = new WorldState(new WorldConfiguration { ResourceCatalog = new ResourceCatalog([definition]) });
        world.Advance(225);
        var node = world.SpawnResourceNode(kind, new Position(0, 0), 100);

        world.Advance(3);

        Assert.False(node.IsAlive);
        Assert.Equal(ResourceDeathCause.Climate, node.CauseOfDeath);
        Assert.Equal(228, node.DeathTick);
    }

    [Fact]
    public void AdvanceResetsColdStressOnceTheClimateBecomesHospitableAgain()
    {
        var kind = new ResourceKindId("frost_intolerant");
        var definition = new ResourceDefinition(
            kind,
            "Frost-Intolerant Plant",
            new SkillTypeId("test"),
            ClimateYields: [new ClimateYield(Climate.Cold, 0f)],
            // Well above the ~75 Cold ticks this test advances through, so the node survives
            // to see the climate turn hospitable rather than withering first.
            TicksToWither: 1000f);
        var world = new WorldState(new WorldConfiguration { ResourceCatalog = new ResourceCatalog([definition]) });
        world.Advance(225);
        var node = world.SpawnResourceNode(kind, new Position(0, 0), 100);
        world.Advance(2);
        Assert.Equal(2f, node.ColdStress);

        // Each Advance-loop iteration uses the season at its *start* tick, so seeing a
        // hospitable climate requires processing tick 300 itself (Spring), one past the
        // 227-299 range that's still Winter/Cold.
        world.Advance(74);

        Assert.Equal(Season.Spring, world.CurrentSeason);
        Assert.True(node.IsAlive);
        Assert.Equal(0f, node.ColdStress);
    }

    [Fact]
    public void AdvanceNeverRegrowsANodeWhileItsColdStressIsAccumulating()
    {
        var kind = new ResourceKindId("frost_intolerant");
        var definition = new ResourceDefinition(
            kind,
            "Frost-Intolerant Plant",
            new SkillTypeId("test"),
            ClimateYields: [new ClimateYield(Climate.Mild, 0f)],
            RegenPerTick: 5f,
            TicksToWither: 100f);
        var world = new WorldState(new WorldConfiguration { ResourceCatalog = new ResourceCatalog([definition]) });
        var node = world.SpawnResourceNode(kind, new Position(0, 0), 200);
        node.RemainingAmount = 50;

        // World starts in Spring (Mild) - inhospitable for this definition despite the
        // otherwise-nonzero global regen multiplier for that climate.
        world.Advance(10);

        Assert.Equal(Season.Spring, world.CurrentSeason);
        Assert.Equal(50f, node.RemainingAmount);
    }

    [Fact]
    public void AdvanceReducesHungerRateInWinterWhenPersonHasInsulatingClothing()
    {
        var world = TestCatalogs.CreateWorld();
        world.Advance(225);
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WarmClothing, 1);

        world.Advance(1);

        Assert.Equal(Season.Winter, world.CurrentSeason);
        Assert.Equal(1f, person.Needs.Hunger);
    }

    [Fact]
    public void AdvanceInsulationNeverReducesHungerRateBelowNormal()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WarmClothing, 1);

        world.Advance(1);

        Assert.Equal(Season.Spring, world.CurrentSeason);
        Assert.Equal(1f, person.Needs.Hunger);
    }
}
