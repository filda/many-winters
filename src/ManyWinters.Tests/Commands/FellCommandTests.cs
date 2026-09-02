using ManyWinters.Core.Commands;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class FellCommandTests
{
    [Fact]
    public void ADeadPersonFellsNothingEvenWithSomebodyElseStandingRightThere()
    {
        var world = TestCatalogs.CreateWorld();
        var living = world.AddPerson("Ava", new Position(3, 4));
        living.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var deceased = world.AddPerson("Bran", new Position(3, 4));
        deceased.KnownTechniques.Add(TestCatalogs.BasicForaging);
        deceased.IsAlive = false;
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(3, 4), 100);

        world.Execute(new FellCommand(deceased.Id, node.Id));

        Assert.True(node.IsAlive);
        Assert.Single(world.ResourceNodes);
    }

    [Fact]
    public void FellingAnAlreadyFelledNodeLeavesTheOnesStillStandingAlone()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(3, 4));
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var felled = world.AddResourceNode(TestCatalogs.Apple, new Position(3, 4), 100);
        var standing = world.AddResourceNode(TestCatalogs.Pear, new Position(3, 4), 100);
        felled.IsAlive = false;

        world.Execute(new FellCommand(person.Id, felled.Id));

        Assert.True(standing.IsAlive);
        Assert.Equal(2, world.ResourceNodes.Count);
    }

    [Fact]
    public void FellingSomethingWhoseLeftoverAmountIsZeroLeavesNothingBehind()
    {
        // A definition that names a leftover kind but no amount would otherwise drop an empty
        // node on the spot - a nothing to walk to and gather nothing from.
        var hollow = new ResourceKindId("hollow_tree");
        var configuration = new WorldConfiguration(
            new ResourceCatalog([
                new ResourceDefinition(hollow, "Hollow Tree", TestCatalogs.Foraging, CanFell: true, FellLeavesKind: TestCatalogs.Wood, FellLeavesAmount: 0f),
            ]),
            TestCatalogs.CreateSkillCatalog(),
            TestCatalogs.CreateRecipeCatalog(),
            TestCatalogs.CreateBuildingCatalog(),
            TestCatalogs.CreateItemCatalog(),
            SeasonParameters.Default);
        var world = new WorldState(configuration);
        var person = world.AddPerson("Ava", new Position(3, 4));
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.AddResourceNode(hollow, new Position(3, 4), 100);

        world.Execute(new FellCommand(person.Id, node.Id));

        Assert.False(node.IsAlive);
        Assert.Single(world.ResourceNodes);
    }

    [Fact]
    public void FellingATreeKillsItAndLeavesAWoodNodeInItsPlace()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(3, 4));
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(3, 4), 100);

        world.Execute(new FellCommand(person.Id, node.Id));

        Assert.False(node.IsAlive);
        Assert.Equal(ResourceDeathCause.Felled, node.CauseOfDeath);
        var leftover = Assert.Single(world.ResourceNodes, n => n.Id != node.Id);
        Assert.Equal(TestCatalogs.Wood, leftover.Kind);
        Assert.Equal(new Position(3, 4), leftover.Position);
        Assert.Equal(TestCatalogs.FellWoodYield, leftover.RemainingAmount);
        Assert.Equal(TestCatalogs.FellWoodYield, leftover.MaxAmount);
        Assert.True(leftover.IsAlive);
    }

    [Fact]
    public void TheWoodLeftBehindByFellingCanBeGatheredLikeAnyOtherNode()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);
        world.Execute(new FellCommand(person.Id, node.Id));
        var leftover = Assert.Single(world.ResourceNodes, n => n.Id != node.Id);

        world.Execute(new GatherCommand(person.Id, leftover.Id));

        Assert.Equal(20, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void FellingATreeRecordsTheDeathTick()
    {
        var world = TestCatalogs.CreateWorld();
        world.Advance(5);
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new FellCommand(person.Id, node.Id));

        Assert.Equal(5, node.DeathTick);
    }

    [Fact]
    public void FellingDoesNotRequireAnyRemainingAmount()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 0);

        world.Execute(new FellCommand(person.Id, node.Id));

        Assert.False(node.IsAlive);
    }

    [Fact]
    public void FellingANonFellableResourceDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var node = world.AddResourceNode(TestCatalogs.Mushroom, new Position(0, 0), 100);

        world.Execute(new FellCommand(person.Id, node.Id));

        Assert.True(node.IsAlive);
        Assert.Single(world.ResourceNodes);
    }

    [Fact]
    public void FellingAnAlreadyDeadNodeDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);
        node.IsAlive = false;

        world.Execute(new FellCommand(person.Id, node.Id));

        Assert.Single(world.ResourceNodes);
    }

    [Fact]
    public void FellingWithoutHavingLearnedTheSkillDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new FellCommand(person.Id, node.Id));

        Assert.True(node.IsAlive);
        Assert.Single(world.ResourceNodes);
    }

    [Fact]
    public void FellingByADeadPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.IsAlive = false;
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new FellCommand(person.Id, node.Id));

        Assert.True(node.IsAlive);
        Assert.Single(world.ResourceNodes);
    }

    [Fact]
    public void FellingAtExactlyTheMaxInteractionDistanceStillWorks()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(WorldState.MaxInteractionDistance, 0), 100);

        world.Execute(new FellCommand(person.Id, node.Id));

        Assert.False(node.IsAlive);
    }

    [Fact]
    public void FellingBeyondTheMaxInteractionDistanceDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(WorldState.MaxInteractionDistance + 1, 0), 100);

        world.Execute(new FellCommand(person.Id, node.Id));

        Assert.True(node.IsAlive);
        Assert.Single(world.ResourceNodes);
    }

    [Fact]
    public void FellingWithAnUnknownPersonOrNodeDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new FellCommand(new PersonId(999), node.Id));
        world.Execute(new FellCommand(person.Id, new ResourceNodeId(999)));

        Assert.True(node.IsAlive);
        Assert.Single(world.ResourceNodes);
    }

    [Fact]
    public void FellingAResourceWithNoFellLeavesKindLeavesNothingBehind()
    {
        var world = new WorldState(WorldConfiguration.Empty with
        {
            ResourceCatalog = new ResourceCatalog(new[]
            {
                new ResourceDefinition(TestCatalogs.Apple, "Apple", TestCatalogs.Foraging, CanFell: true),
            }),
            SkillCatalog = new SkillCatalog(new[]
            {
                new SkillDefinition(TestCatalogs.Foraging, "Foraging", TestCatalogs.BasicForaging, TestCatalogs.EfficientForaging),
            }),
        });
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new FellCommand(person.Id, node.Id));

        Assert.False(node.IsAlive);
        Assert.Single(world.ResourceNodes);
    }
}
