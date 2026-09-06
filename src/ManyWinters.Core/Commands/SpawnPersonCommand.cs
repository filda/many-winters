using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record SpawnPersonCommand(
    string Name,
    Position Position,
    long InitialAgeTicks = 0,
    PersonId? MotherId = null,
    PersonId? FatherId = null) : ICommand
{
    public void Execute(WorldState world) => world.AddPerson(new Person
    {
        Id = world.NextPersonId,
        Name = Name,
        Position = Position,
        BirthTick = world.Clock.CurrentTick - InitialAgeTicks,
        MotherId = MotherId,
        FatherId = FatherId,
    });
}
