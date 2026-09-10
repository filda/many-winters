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
        var living = world.SpawnPerson("Ava", new Position(3, 4));
        living.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var deceased = world.SpawnPerson("Bran", new Position(3, 4));
        deceased.KnownTechniques.Add(TestCatalogs.BasicForaging);
        deceased.IsAlive = false;
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(3, 4), 100);

        world.Execute(new FellCommand(deceased, node));

        Assert.True(node.IsAlive);
        Assert.Single(world.ResourceNodes);
    }

    [Fact]
    public void FellingAnAlreadyFelledNodeLeavesTheOnesStillStandingAlone()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(3, 4));
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var felled = world.SpawnResourceNode(TestCatalogs.Apple, new Position(3, 4), 100);
        var standing = world.SpawnResourceNode(TestCatalogs.Pear, new Position(3, 4), 100);
        felled.IsAlive = false;

        world.Execute(new FellCommand(person, felled));

        Assert.True(standing.IsAlive);
        Assert.Equal(2, world.ResourceNodes.Count);
    }

    [Fact]
    public void FellingSomethingWhoseLeftoverAmountIsZeroLeavesNothingBehind()
    {
        // A definition that names a leftover kind but no amount would otherwise drop an empty
        // node on the spot - a nothing to walk to and gather nothing from.
        var hollow = new ResourceKindId("hollow_tree");
        var configuration = TestCatalogs.CreateConfiguration() with
        {
            ResourceCatalog = new ResourceCatalog([
                new ResourceDefinition(hollow, "Hollow Tree", TestCatalogs.Foraging, CanFell: true, FellLeaves: [new(TestCatalogs.Wood, 0f)]),
            ]),
        };
        var world = new WorldState(configuration);
        var person = world.SpawnPerson("Ava", new Position(3, 4));
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.SpawnResourceNode(hollow, new Position(3, 4), 100);

        world.Execute(new FellCommand(person, node));

        Assert.False(node.IsAlive);
        Assert.Single(world.ResourceNodes);
    }

    [Fact]
    public void FellingATreeKillsItAndLeavesAWoodNodeInItsPlace()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(3, 4));
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(3, 4), 100);

        world.Execute(new FellCommand(person, node));

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
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);
        world.Execute(new FellCommand(person, node));
        var leftover = Assert.Single(world.ResourceNodes, n => n.Id != node.Id);

        world.Execute(new GatherCommand(person, leftover));

        Assert.Equal(20, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void FellingAForestTreeLeavesBothAStumpInPlaceAndAFallenLogNearby()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(3, 4));
        person.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);
        var node = world.SpawnResourceNode(TestCatalogs.ConiferTree, new Position(3, 4), 100);

        world.Execute(new FellCommand(person, node));

        Assert.Equal(3, world.ResourceNodes.Count);
        var stump = Assert.Single(world.ResourceNodes, n => n.Kind == TestCatalogs.TreeStump);
        Assert.Equal(new Position(3, 4), stump.Position);
        var log = Assert.Single(world.ResourceNodes, n => n.Kind == TestCatalogs.FallenLog);
        Assert.NotEqual(new Position(3, 4), log.Position);
    }

    [Fact]
    public void FellingATreeRecordsTheDeathTick()
    {
        var world = TestCatalogs.CreateWorld();
        world.Advance(5);
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new FellCommand(person, node));

        Assert.Equal(5, node.DeathTick);
    }

    [Fact]
    public void FellingDoesNotRequireAnyRemainingAmount()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 0);

        world.Execute(new FellCommand(person, node));

        Assert.False(node.IsAlive);
    }

    [Fact]
    public void FellingANonFellableResourceDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        var node = world.SpawnResourceNode(TestCatalogs.Mushroom, new Position(0, 0), 100);

        world.Execute(new FellCommand(person, node));

        Assert.True(node.IsAlive);
        Assert.Single(world.ResourceNodes);
    }

    [Fact]
    public void FellingAnAlreadyDeadNodeDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);
        node.IsAlive = false;

        world.Execute(new FellCommand(person, node));

        Assert.Single(world.ResourceNodes);
    }

    [Fact]
    public void FellingWithoutHavingLearnedTheSkillDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new FellCommand(person, node));

        Assert.True(node.IsAlive);
        Assert.Single(world.ResourceNodes);
    }

    [Fact]
    public void FellingByADeadPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.IsAlive = false;
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new FellCommand(person, node));

        Assert.True(node.IsAlive);
        Assert.Single(world.ResourceNodes);
    }

    [Fact]
    public void FellingAtExactlyTheMaxInteractionDistanceStillWorks()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(world.Configuration.Rules.MaxInteractionDistance, 0), 100);

        world.Execute(new FellCommand(person, node));

        Assert.False(node.IsAlive);
    }

    [Fact]
    public void FellingBeyondTheMaxInteractionDistanceDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0), 100);

        world.Execute(new FellCommand(person, node));

        Assert.True(node.IsAlive);
        Assert.Single(world.ResourceNodes);
    }

    [Fact]
    public void FellingAResourceWithNoFellLeavesKindLeavesNothingBehind()
    {
        var world = new WorldState(new WorldConfiguration
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
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 100);

        world.Execute(new FellCommand(person, node));

        Assert.False(node.IsAlive);
        Assert.Single(world.ResourceNodes);
    }
}
