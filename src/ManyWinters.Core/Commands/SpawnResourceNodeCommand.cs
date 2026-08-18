using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record SpawnResourceNodeCommand(ResourceKind Kind, Position Position, float Amount) : ICommand
{
    public void Execute(WorldState world) => world.AddResourceNode(Kind, Position, Amount);
}
