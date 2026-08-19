using ManyWinters.Core.Construction;
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
}
