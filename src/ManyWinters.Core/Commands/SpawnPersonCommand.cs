using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Mother and Father are required, not optional - there's no such thing as a person without
// parents (see Person.Mother); a caller who genuinely has nobody to name says so with
// Person.Unknown. The id is normally the person's own to draw (see EntityId) - only a creator
// that has to produce the same world twice (MapLoader) names one.
public sealed record SpawnPersonCommand(
    PersonId Id,
    string Name,
    Position Position,
    Person Mother,
    Person Father,
    long InitialAgeTicks = 0) : ICommand
{
    public SpawnPersonCommand(string name, Position position, Person mother, Person father, long initialAgeTicks = 0)
        : this(PersonId.New(), name, position, mother, father, initialAgeTicks)
    {
    }

    public void Execute(WorldState world) => world.AddPerson(new Person
    {
        Id = Id,
        Name = Name,
        Position = Position,
        BirthTick = world.Clock.CurrentTick - InitialAgeTicks,
        Mother = Mother,
        Father = Father,
    });
}
