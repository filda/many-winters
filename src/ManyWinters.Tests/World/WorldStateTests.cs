using ManyWinters.Core.Construction;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

public class WorldStateTests
{
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
    public void AddGraveAssignsSequentialUniqueIds()
    {
        var world = new WorldState();

        var first = world.AddGrave(new Position(0, 0), isMarked: false, name: null, ageAtDeath: null, knownTechniques: []);
        var second = world.AddGrave(new Position(1, 1), isMarked: false, name: null, ageAtDeath: null, knownTechniques: []);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(1, first.Id.Value);
        Assert.Equal(2, second.Id.Value);
    }

    [Fact]
    public void AddGraveTracksItInGraves()
    {
        var world = new WorldState();

        var grave = world.AddGrave(new Position(2, 3), isMarked: true, name: "Ava", ageAtDeath: 5, knownTechniques: []);

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

        var grave = world.AddGrave(new Position(0, 0), isMarked: false, name: null, ageAtDeath: null, knownTechniques: []);

        Assert.Same(grave, raised);
    }

    [Fact]
    public void AddGraveDoesNotThrowWhenNothingIsSubscribedToGraveAdded()
    {
        var world = new WorldState();

        world.AddGrave(new Position(0, 0), isMarked: false, name: null, ageAtDeath: null, knownTechniques: []);
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
