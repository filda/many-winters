using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Mother and Father are required, not optional - there's no such thing as a person without
// parents (see Person.Mother); a caller who genuinely has nobody to name says so with
// Person.Unknown.
public sealed record SpawnPersonCommand(
    string Name,
    Position Position,
    Person Mother,
    Person Father,
    long InitialAgeTicks = 0) : ICommand
{
    public void Execute(WorldState world) => world.AddPerson(new Person
    {
        Id = world.NextPersonId,
        Name = Name,
        Position = Position,
        BirthTick = world.Clock.CurrentTick - InitialAgeTicks,
        Mother = Mother,
        Father = Father,
    });
}
