using ManyWinters.Core.Commands;
using ManyWinters.Core.Construction;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
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
    public void AddPersonAssignsSequentialUniqueIds()
    {
        var world = new WorldState();

        var first = world.AddPerson("Ava", new Position(0, 0));
        var second = world.AddPerson("Bran", new Position(1, 1));

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(1, first.Id.Value);
        Assert.Equal(2, second.Id.Value);
    }

    [Fact]
    public void AddPersonWithAnInitialAgeBackdatesTheBirthTick()
    {
        var world = new WorldState();
        world.Clock.Advance(1000);

        var person = world.AddPerson("Ava", new Position(0, 0), initialAgeTicks: 300);

        Assert.Equal(700, person.BirthTick);
        Assert.Equal(1, world.AgeInYears(person));
    }

    [Fact]
    public void AddPersonTracksThemInPeople()
    {
        var world = new WorldState();

        world.AddPerson("Ava", new Position(0, 0));
        world.AddPerson("Bran", new Position(1, 1));

        Assert.Equal(2, world.People.Count);
    }

    [Fact]
    public void NewWorldHasNoPeopleAndTickZero()
    {
        var world = new WorldState();

        Assert.Empty(world.People);
        Assert.Equal(0, world.Clock.CurrentTick);
    }

    [Fact]
    public void AdvanceMovesTheClockForward()
    {
        var world = new WorldState();

        world.Advance(5);

        Assert.Equal(5, world.Clock.CurrentTick);
    }

    [Fact]
    public void AdvanceIncreasesHungerForEveryPerson()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));

        world.Advance(3);

        Assert.Equal(3f, person.Needs.Hunger);
        Assert.True(person.IsAlive);
    }

    [Fact]
    public void AdvanceClampsHungerAtItsMaximum()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));

        world.Advance(1000);

        Assert.Equal(100f, person.Needs.Hunger);
    }

    [Fact]
    public void AdvanceKillsAPersonWhoseHungerReachesTheMaximum()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));

        world.Advance(99);
        Assert.True(person.IsAlive);

        world.Advance(1);

        Assert.False(person.IsAlive);
    }

    [Fact]
    public void AgeInYearsIsZeroForANewbornPerson()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));

        Assert.Equal(0, world.AgeInYears(person));
    }

    [Fact]
    public void AgeInYearsIncreasesAfterAFullYearPasses()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));

        world.Advance(WorldState.TicksPerYear);

        Assert.Equal(1, world.AgeInYears(person));
    }

    [Fact]
    public void AgeInSeasonsIsZeroForANewbornPerson()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));

        Assert.Equal(0, world.AgeInSeasons(person));
    }

    [Fact]
    public void AgeInSeasonsIncreasesAfterASeasonPasses()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));

        world.Advance(75);

        Assert.Equal(1, world.AgeInSeasons(person));
    }

    [Fact]
    public void AgeInSeasonsAccountsForThePersonsBirthTickNotJustElapsedWorldTime()
    {
        var world = new WorldState();
        world.Advance(75);
        var person = world.AddPerson("Ava", new Position(0, 0));

        world.Advance(75);

        Assert.Equal(1, world.AgeInSeasons(person));
    }

    [Fact]
    public void AgeInYearsAccountsForThePersonsBirthTickNotJustElapsedWorldTime()
    {
        var world = new WorldState();
        world.Advance(WorldState.TicksPerYear);
        var person = world.AddPerson("Ava", new Position(0, 0));

        world.Advance(WorldState.TicksPerYear);

        Assert.Equal(1, world.AgeInYears(person));
    }

    [Fact]
    public void MaxCarryWeightForAnAdultWithNoGearIsTheAdultBaseline()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);

        Assert.Equal(CarryCapacity.AdultBaseWeight, world.MaxCarryWeightFor(person));
    }

    [Fact]
    public void MaxCarryWeightForAddsTheBonusOfGearCurrentlyHeld()
    {
        var bag = new ItemKindId("bag");
        var world = new WorldState(WorldConfiguration.Empty with
        {
            ItemCatalog = new ItemCatalog(new[] { new ItemDefinition(bag, "Bag", CarryCapacityBonus: 20f) }),
        });
        var person = world.AddPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.Inventory.Add(bag, 1);

        Assert.Equal(CarryCapacity.AdultBaseWeight + 20f, world.MaxCarryWeightFor(person));
    }

    [Fact]
    public void MaxCarryWeightForGearBonusDoesNotStackWithMoreCopiesOfTheSameItem()
    {
        var bag = new ItemKindId("bag");
        var world = new WorldState(WorldConfiguration.Empty with
        {
            ItemCatalog = new ItemCatalog(new[] { new ItemDefinition(bag, "Bag", CarryCapacityBonus: 20f) }),
        });
        var person = world.AddPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.Inventory.Add(bag, 3);

        Assert.Equal(CarryCapacity.AdultBaseWeight + 20f, world.MaxCarryWeightFor(person));
    }

    [Fact]
    public void AdvanceGivesAPersonWithNoOrdersAnIdleTaskInsteadOfLeavingThemFrozen()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var start = person.Position;

        world.Advance(20);

        Assert.IsType<IdleTask>(person.Tasks.Current);
        Assert.NotEqual(start, person.Position);
    }

    [Fact]
    public void AdvanceDoesNotReplaceAnAlreadyInProgressTaskWithIdle()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));
        world.Execute(new MoveCommand(person.Id, new Position(100, 0)));

        world.Advance(1);

        Assert.IsType<MoveTask>(person.Tasks.Current);
    }

    [Fact]
    public void AdvanceDoesNotStartIdleWanderingWhileAGracePeriodIsStillRunning()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));
        world.Execute(new GrantIdleGraceCommand(person.Id, 5));
        var start = person.Position;

        world.Advance(4);

        Assert.Null(person.Tasks.Current);
        Assert.Equal(start, person.Position);
    }

    [Fact]
    public void AdvanceStartsIdleWanderingAsSoonAsTheGracePeriodRunsOut()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));
        world.Execute(new GrantIdleGraceCommand(person.Id, 5));

        world.Advance(5);

        Assert.IsType<IdleTask>(person.Tasks.Current);
    }

    [Fact]
    public void AdvanceAssignsTheExactCurrentTickWhenDeathOccursMidwayThroughAMultiTickAdvance()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Needs.Hunger = 98;

        world.Advance(3);

        Assert.Equal(2, person.DeathTick);
    }

    [Fact]
    public void AdvanceDoesNotKeepUpdatingDeathTickForAnAlreadyDeadPerson()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));

        world.Advance(100);
        Assert.Equal(100, person.DeathTick);

        world.Advance(10);

        Assert.Equal(100, person.DeathTick);
    }

    [Fact]
    public void AdvanceSubtractsBirthTickRatherThanAddingItWhenCheckingOldAgeDeath()
    {
        var world = new WorldState();
        world.Clock.Advance(2900);
        var person = world.AddPerson("Ava", new Position(0, 0));

        world.Advance(1);

        Assert.True(person.IsAlive);
    }

    [Fact]
    public void AdvanceKillsAPersonWhoReachesTheMaximumLifespanEvenWhenNeverHungry()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));

        // MaxLifespanYears is 10; feed the person back to zero after every tick so only
        // old age - never hunger - can be responsible for their death.
        for (var tick = 0; tick < (WorldState.TicksPerYear * 10) - 1; tick++)
        {
            world.Advance(1);
            person.Needs.Hunger = 0;
        }

        Assert.True(person.IsAlive);

        world.Advance(1);

        Assert.False(person.IsAlive);
        Assert.Equal(WorldState.TicksPerYear * 10, person.DeathTick);
    }

    [Fact]
    public void AdvanceRecordsHungerAsTheCauseOfDeathWhenHungerReachesMaximum()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));

        world.Advance(100);

        Assert.Equal(DeathCause.Hunger, person.CauseOfDeath);
    }

    [Fact]
    public void AdvanceRecordsOldAgeAsTheCauseOfDeathWhenTheMaximumLifespanIsReached()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));

        for (var tick = 0; tick < (WorldState.TicksPerYear * 10) - 1; tick++)
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
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));

        for (var tick = 0; tick < (WorldState.TicksPerYear * 10) - 1; tick++)
        {
            world.Advance(1);
            person.Needs.Hunger = 0;
        }

        person.Needs.Hunger = 99;
        world.Advance(1);

        Assert.Equal(DeathCause.OldAge, person.CauseOfDeath);
    }

    [Fact]
    public void AdvanceLeavesADeceasedPersonsInventoryUntouchedRatherThanTransferringItAutomatically()
    {
        var world = new WorldState();
        var parent = world.AddPerson("Ava", new Position(0, 0));
        parent.Needs.Hunger = 99;
        parent.Inventory.Add(TestCatalogs.WoodItem, 5);
        var child = world.AddPerson("Bran", new Position(0, 0), motherId: parent.Id);

        world.Advance(1);

        Assert.False(parent.IsAlive);
        Assert.Equal(5, parent.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(0, child.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void AddPersonAssignsMotherAndFatherIdsWhenProvided()
    {
        var world = new WorldState();
        var mother = world.AddPerson("Sela", new Position(0, 0));
        var father = world.AddPerson("Bran", new Position(0, 0));

        var child = world.AddPerson("Ava", new Position(0, 0), motherId: mother.Id, fatherId: father.Id);

        Assert.Equal(mother.Id, child.MotherId);
        Assert.Equal(father.Id, child.FatherId);
    }

    [Fact]
    public void AddPersonLeavesMotherAndFatherIdsNullByDefault()
    {
        var world = new WorldState();

        var person = world.AddPerson("Ava", new Position(0, 0));

        Assert.Null(person.MotherId);
        Assert.Null(person.FatherId);
    }

    [Fact]
    public void AddGraveAssignsSequentialUniqueIds()
    {
        var world = new WorldState();

        var first = world.AddGrave(new Position(0, 0), isMarked: false, name: null, ageAtDeath: null, causeOfDeath: null, motherName: null, fatherName: null, knownTechniques: []);
        var second = world.AddGrave(new Position(1, 1), isMarked: false, name: null, ageAtDeath: null, causeOfDeath: null, motherName: null, fatherName: null, knownTechniques: []);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(1, first.Id.Value);
        Assert.Equal(2, second.Id.Value);
    }

    [Fact]
    public void AddGraveTracksItInGraves()
    {
        var world = new WorldState();

        var grave = world.AddGrave(new Position(2, 3), isMarked: true, name: "Ava", ageAtDeath: 5, causeOfDeath: null, motherName: null, fatherName: null, knownTechniques: []);

        var tracked = Assert.Single(world.Graves);
        Assert.Same(grave, tracked);
        Assert.Equal(new Position(2, 3), tracked.Position);
        Assert.True(tracked.IsMarked);
        Assert.Equal("Ava", tracked.Name);
        Assert.Equal(5, tracked.AgeAtDeath);
    }

    [Fact]
    public void AddGraveRaisesGraveAddedWithTheNewGrave()
    {
        var world = new WorldState();
        Grave? raised = null;
        world.GraveAdded += g => raised = g;

        var grave = world.AddGrave(new Position(0, 0), isMarked: false, name: null, ageAtDeath: null, causeOfDeath: null, motherName: null, fatherName: null, knownTechniques: []);

        Assert.Same(grave, raised);
    }

    [Fact]
    public void AddGraveDoesNotThrowWhenNothingIsSubscribedToGraveAdded()
    {
        var world = new WorldState();

        world.AddGrave(new Position(0, 0), isMarked: false, name: null, ageAtDeath: null, causeOfDeath: null, motherName: null, fatherName: null, knownTechniques: []);
    }

    [Fact]
    public void AdvanceMovesAPersonWithAnActiveMoveTaskTowardTheirDestination()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));
        world.Execute(new MoveCommand(person.Id, new Position(10, 0)));

        world.Advance(3);

        Assert.Equal(new Position(3, 0), person.Position);
    }

    [Fact]
    public void AdvanceStopsMovingAPersonOnceTheyReachTheirDestination()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));
        world.Execute(new MoveCommand(person.Id, new Position(2, 0)));

        world.Advance(2);

        Assert.Equal(new Position(2, 0), person.Position);
    }

    [Fact]
    public void AdvanceLetsAPersonIdleWanderOnceTheyReachTheirDestinationInsteadOfFreezingThere()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));
        world.Execute(new MoveCommand(person.Id, new Position(2, 0)));

        world.Advance(20);

        Assert.IsType<IdleTask>(person.Tasks.Current);
        Assert.NotEqual(new Position(2, 0), person.Position);
    }

    [Fact]
    public void AdvanceStopsMovingAPersonOnceTheyDieFromHunger()
    {
        var world = new WorldState();
        var person = world.AddPerson("Ava", new Position(0, 0));
        world.Execute(new MoveCommand(person.Id, new Position(1000, 0)));

        world.Advance(100);
        Assert.False(person.IsAlive);
        var positionAtDeath = person.Position;

        world.Advance(10);

        Assert.Equal(positionAtDeath, person.Position);
    }

    [Fact]
    public void AddResourceNodeAssignsSequentialUniqueIds()
    {
        var world = new WorldState();

        var first = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 50);
        var second = world.AddResourceNode(TestCatalogs.Apple, new Position(1, 1), 50);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(1, first.Id.Value);
        Assert.Equal(2, second.Id.Value);
    }

    [Fact]
    public void AddResourceNodeTracksItInResourceNodes()
    {
        var world = new WorldState();

        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(2, 3), 40);

        var tracked = Assert.Single(world.ResourceNodes);
        Assert.Same(node, tracked);
        Assert.Equal(TestCatalogs.Apple, tracked.Kind);
        Assert.Equal(new Position(2, 3), tracked.Position);
        Assert.Equal(40f, tracked.RemainingAmount);
    }

    [Fact]
    public void AddResourceNodeSetsMaxAmountToTheSpawnedAmount()
    {
        var world = new WorldState();

        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 40);

        Assert.Equal(40f, node.MaxAmount);
    }

    [Fact]
    public void AddPersonRaisesPersonAddedWithTheNewPerson()
    {
        var world = new WorldState();
        Person? raised = null;
        world.PersonAdded += p => raised = p;

        var person = world.AddPerson("Ava", new Position(0, 0));

        Assert.Same(person, raised);
    }

    [Fact]
    public void AddPersonDoesNotThrowWhenNothingIsSubscribedToPersonAdded()
    {
        var world = new WorldState();

        world.AddPerson("Ava", new Position(0, 0));
    }

    [Fact]
    public void AddResourceNodeRaisesResourceNodeAddedWithTheNewNode()
    {
        var world = new WorldState();
        ResourceNode? raised = null;
        world.ResourceNodeAdded += n => raised = n;

        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(2, 3), 40);

        Assert.Same(node, raised);
    }

    [Fact]
    public void AddResourceNodeDoesNotThrowWhenNothingIsSubscribedToResourceNodeAdded()
    {
        var world = new WorldState();

        world.AddResourceNode(TestCatalogs.Apple, new Position(2, 3), 40);
    }

    [Fact]
    public void AddBuildingAssignsSequentialUniqueIds()
    {
        var world = new WorldState();

        var first = world.AddBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        var second = world.AddBuilding(TestCatalogs.StorageHut, new Position(1, 1));

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(1, first.Id.Value);
        Assert.Equal(2, second.Id.Value);
    }

    [Fact]
    public void AddBuildingTracksItInBuildings()
    {
        var world = new WorldState();

        var building = world.AddBuilding(TestCatalogs.StorageHut, new Position(2, 3));

        var tracked = Assert.Single(world.Buildings);
        Assert.Same(building, tracked);
        Assert.Equal(TestCatalogs.StorageHut, tracked.Kind);
        Assert.Equal(new Position(2, 3), tracked.Position);
    }

    [Fact]
    public void AddBuildingStartsWithAnEmptyInventory()
    {
        var world = new WorldState();

        var building = world.AddBuilding(TestCatalogs.StorageHut, new Position(0, 0));

        Assert.Empty(building.Inventory.Counts);
    }

    [Fact]
    public void AddBuildingRaisesBuildingAddedWithTheNewBuilding()
    {
        var world = new WorldState();
        Building? raised = null;
        world.BuildingAdded += b => raised = b;

        var building = world.AddBuilding(TestCatalogs.StorageHut, new Position(2, 3));

        Assert.Same(building, raised);
    }

    [Fact]
    public void AddBuildingDoesNotThrowWhenNothingIsSubscribedToBuildingAdded()
    {
        var world = new WorldState();

        world.AddBuilding(TestCatalogs.StorageHut, new Position(2, 3));
    }

    [Fact]
    public void AddBuildingStartsAtFullCondition()
    {
        var world = new WorldState();

        var building = world.AddBuilding(TestCatalogs.StorageHut, new Position(0, 0));

        Assert.Equal(100f, building.Condition);
    }

    [Fact]
    public void AdvanceDecaysBuildingCondition()
    {
        var world = new WorldState();
        var building = world.AddBuilding(TestCatalogs.StorageHut, new Position(0, 0));

        world.Advance(100);

        Assert.Equal(95f, building.Condition);
    }

    [Fact]
    public void AdvanceNeverDecaysBuildingConditionBelowZero()
    {
        var world = new WorldState();
        var building = world.AddBuilding(TestCatalogs.StorageHut, new Position(0, 0));

        world.Advance(1_000_000);

        Assert.Equal(0f, building.Condition);
    }

    [Fact]
    public void NewWorldStartsInSpring()
    {
        var world = new WorldState();

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
        var world = new WorldState();

        world.Advance(tick);

        Assert.Equal(expected, world.CurrentSeason);
    }

    [Fact]
    public void AdvanceAppliesDoubleHungerRateDuringWinter()
    {
        var world = new WorldState();
        world.Advance(225);
        var person = world.AddPerson("Ava", new Position(0, 0));

        world.Advance(1);

        Assert.Equal(Season.Winter, world.CurrentSeason);
        Assert.Equal(2f, person.Needs.Hunger);
    }

    [Fact]
    public void AdvanceAcrossTheWinterBoundaryAppliesEachTicksOwnRate()
    {
        var world = new WorldState();
        world.Advance(224);
        var person = world.AddPerson("Ava", new Position(0, 0));

        // Ticks 224 (Autumn) and 225 (Winter): 1 + 2 = 3 hunger, not a flat rate for both.
        world.Advance(2);

        Assert.Equal(3f, person.Needs.Hunger);
    }

    [Fact]
    public void AdvanceRegrowsADepletedResourceNodeTowardItsMaxAmount()
    {
        var world = TestCatalogs.CreateWorld();
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 200);
        node.RemainingAmount = 50;

        world.Advance(10);

        Assert.Equal(50f + (TestCatalogs.FoodRegenPerTick * 10), node.RemainingAmount);
    }

    [Fact]
    public void AdvanceNeverRegrowsAResourceNodeAboveItsMaxAmount()
    {
        var world = TestCatalogs.CreateWorld();
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 200);
        node.RemainingAmount = 199;

        world.Advance(10);

        Assert.Equal(200f, node.RemainingAmount);
    }

    [Fact]
    public void AdvanceDoesNotRegrowResourceNodesDuringWinter()
    {
        var world = TestCatalogs.CreateWorld();
        world.Advance(225);
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 200);
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
        var world = new WorldState(WorldConfiguration.Empty with { ResourceCatalog = new ResourceCatalog([definition]) });
        world.Advance(225);
        var node = world.AddResourceNode(kind, new Position(0, 0), 100);

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
        var world = new WorldState(WorldConfiguration.Empty with { ResourceCatalog = new ResourceCatalog([definition]) });
        world.Advance(225);
        var node = world.AddResourceNode(kind, new Position(0, 0), 100);

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
        var world = new WorldState(WorldConfiguration.Empty with { ResourceCatalog = new ResourceCatalog([definition]) });
        world.Advance(225);
        var node = world.AddResourceNode(kind, new Position(0, 0), 100);
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
        var world = new WorldState(WorldConfiguration.Empty with { ResourceCatalog = new ResourceCatalog([definition]) });
        var node = world.AddResourceNode(kind, new Position(0, 0), 200);
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
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WarmClothing, 1);

        world.Advance(1);

        Assert.Equal(Season.Winter, world.CurrentSeason);
        Assert.Equal(1f, person.Needs.Hunger);
    }

    [Fact]
    public void AdvanceInsulationNeverReducesHungerRateBelowNormal()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.WarmClothing, 1);

        world.Advance(1);

        Assert.Equal(Season.Spring, world.CurrentSeason);
        Assert.Equal(1f, person.Needs.Hunger);
    }
}
