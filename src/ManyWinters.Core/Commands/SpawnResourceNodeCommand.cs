using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// A freshly spawned node is full: MaxAmount is what it regenerates back toward (see
// WorldState.Advance), so it starts out at exactly that.
public sealed record SpawnResourceNodeCommand(ResourceKindId Kind, Position Position, float Amount) : ICommand
{
    public void Execute(WorldState world) => world.AddResourceNode(new ResourceNode
    {
        Id = world.NextResourceNodeId,
        Kind = Kind,
        Position = Position,
        RemainingAmount = Amount,
        MaxAmount = Amount,
    });
}
