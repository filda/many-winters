using ManyWinters.Core.Commands;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class SpawnResourceNodeCommandTests
{
    private static List<Entity> ResourceNodes(WorldState world) =>
        world.Entities.Where(e => e.Category == EntityCategory.Growable).ToList();

    [Fact]
    public void ExecuteAddsAResourceNodeWithTheGivenKindPositionAndAmount()
    {
        var world = TestCatalogs.CreateWorld();

        world.Execute(new SpawnResourceNodeCommand(TestCatalogs.Apple, new Position(3, 4), 50f));

        var node = Assert.Single(ResourceNodes(world));
        Assert.Equal(TestCatalogs.Apple, node.Kind);
        Assert.Equal(new Position(3, 4), node.Position);
        Assert.Equal(50f, node.Growth!.RemainingAmount);
    }

    [Fact]
    public void ExecuteSpawnsTheNodeFullWithMaxAmountEqualToTheGivenAmount()
    {
        var world = TestCatalogs.CreateWorld();

        world.Execute(new SpawnResourceNodeCommand(TestCatalogs.Apple, new Position(0, 0), 40f));

        var node = Assert.Single(ResourceNodes(world));
        Assert.Equal(40f, node.Growth!.MaxAmount);
        Assert.Equal(node.Growth.MaxAmount, node.Growth.RemainingAmount);
    }

    [Fact]
    public void ExecutingTwiceAddsTwoDistinctNodes()
    {
        var world = TestCatalogs.CreateWorld();

        world.Execute(new SpawnResourceNodeCommand(TestCatalogs.Apple, new Position(0, 0), 10f));
        world.Execute(new SpawnResourceNodeCommand(TestCatalogs.Apple, new Position(1, 1), 10f));

        var nodes = ResourceNodes(world);
        Assert.Equal(2, nodes.Count);
        Assert.NotEqual(nodes[0].Id, nodes[1].Id);
    }

    // World-building, not a player action: there is no state in which it refuses.
    [Fact]
    public void NothingEverBlocksSpawningAResourceNode()
    {
        var world = TestCatalogs.CreateWorld();

        var command = new SpawnResourceNodeCommand(TestCatalogs.Apple, new Position(0, 0), 100);

        Assert.Equal(ActionBlocker.None, command.Blocker(world));
    }
}
