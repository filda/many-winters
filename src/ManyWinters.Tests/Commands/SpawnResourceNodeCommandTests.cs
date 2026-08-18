using ManyWinters.Core.Commands;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.Commands;

public class SpawnResourceNodeCommandTests
{
    [Fact]
    public void ExecuteAddsAResourceNodeWithTheGivenKindPositionAndAmount()
    {
        var world = new WorldState();

        world.Execute(new SpawnResourceNodeCommand(ResourceKind.Food, new Position(3, 4), 50f));

        var node = Assert.Single(world.ResourceNodes);
        Assert.Equal(ResourceKind.Food, node.Kind);
        Assert.Equal(new Position(3, 4), node.Position);
        Assert.Equal(50f, node.RemainingAmount);
    }

    [Fact]
    public void ExecutingTwiceAddsTwoDistinctNodes()
    {
        var world = new WorldState();

        world.Execute(new SpawnResourceNodeCommand(ResourceKind.Food, new Position(0, 0), 10f));
        world.Execute(new SpawnResourceNodeCommand(ResourceKind.Food, new Position(1, 1), 10f));

        Assert.Equal(2, world.ResourceNodes.Count);
        Assert.NotEqual(world.ResourceNodes[0].Id, world.ResourceNodes[1].Id);
    }
}
