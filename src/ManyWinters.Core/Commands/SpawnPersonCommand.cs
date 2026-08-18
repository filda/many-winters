using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record SpawnPersonCommand(string Name, Position Position) : ICommand
{
    public void Execute(WorldState world) => world.AddPerson(Name, Position);
}
