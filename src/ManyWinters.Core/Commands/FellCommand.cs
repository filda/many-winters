using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Destroys a fellable resource node (a fruit tree, say), leaving behind a one-time pile of
// whatever ResourceDefinition.FellLeavesKind says (typically wood) that still has to be
// gathered - unlike GatherCommand, which takes from the node repeatedly and leaves it standing.
public sealed record FellCommand(PersonId PersonId, ResourceNodeId ResourceNodeId) : ICommand
{
    public void Execute(WorldState world)
    {
        var person = world.People.FirstOrDefault(p => p.Id == PersonId && p.IsAlive);
        var node = world.ResourceNodes.FirstOrDefault(n => n.Id == ResourceNodeId && n.IsAlive);
        if (person is null || node is null || WorldState.Distance(person.Position, node.Position) > WorldState.MaxInteractionDistance)
        {
            return;
        }

        var resource = world.ResourceCatalog.Get(node.Kind);
        if (!resource.CanFell)
        {
            return;
        }

        node.IsAlive = false;
        node.DeathTick = world.Clock.CurrentTick;
        node.CauseOfDeath = ResourceDeathCause.Felled;

        if (resource.FellLeavesKind is { } leftoverKind && resource.FellLeavesAmount > 0)
        {
            world.AddResourceNode(leftoverKind, node.Position, resource.FellLeavesAmount);
        }
    }
}
