using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// A freshly spawned node is full: MaxAmount is what it regenerates back toward (see
// WorldState.Advance), so it starts out at exactly that. The id is normally the node's own to
// draw (see EntityId) - only a creator that has to produce the same world twice (MapLoader)
// names one.
public sealed record SpawnResourceNodeCommand(ResourceNodeId Id, ResourceKindId Kind, Position Position, float Amount) : ICommand
{
    public SpawnResourceNodeCommand(ResourceKindId kind, Position position, float amount)
        : this(ResourceNodeId.New(), kind, position, amount)
    {
    }

    public void Execute(WorldState world) => world.AddResourceNode(new ResourceNode
    {
        Id = Id,
        Kind = Kind,
        Position = Position,
        RemainingAmount = Amount,
        MaxAmount = Amount,
    });
}
