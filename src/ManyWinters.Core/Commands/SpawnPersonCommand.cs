using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record SpawnPersonCommand(
    string Name,
    Position Position,
    long InitialAgeTicks = 0,
    PersonId? MotherId = null,
    PersonId? FatherId = null) : ICommand
{
    public void Execute(WorldState world) => world.AddPerson(Name, Position, InitialAgeTicks, MotherId, FatherId);
}
