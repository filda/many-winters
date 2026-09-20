using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Mother and Father are required (see Person.Mother); a caller with nobody to name passes
// Person.Unknown. The id is normally the person's own to draw (see EntityId) - only a creator
// that must produce the same world twice (MapLoader) names one.
public sealed record SpawnPersonCommand(
    PersonId Id,
    string Name,
    Position Position,
    Person Mother,
    Person Father,
    long InitialAgeTicks = 0,
    // Null lets the id decide (see Person.Sex). MapLoader sets it: its family table has already
    // settled who bore whom.
    Sex? Sex = null,
    // Null takes the player band's rate from the rules; an NPC band passes its own (see
    // Person.Curiosity).
    float? Curiosity = null) : ICommand
{
    public SpawnPersonCommand(string name, Position position, Person mother, Person father, long initialAgeTicks = 0)
        : this(PersonId.New(), name, position, mother, father, initialAgeTicks)
    {
    }

    // World-building, not a player action: whoever calls this is creating the world rather than
    // acting inside it, so there is nothing to refuse.
    public ActionBlocker Blocker(WorldState world) => ActionBlocker.None;

    public void Execute(WorldState world) => world.AddPerson(new Person
    {
        Id = Id,
        Name = Name,
        Position = Position,
        BirthTick = world.Clock.CurrentTick - InitialAgeTicks,
        Mother = Mother,
        Father = Father,
        Sex = Sex ?? Person.SexOf(Id),
        MaxHunger = world.Configuration.Rules.MaxHungerFor(Id),
        Curiosity = Curiosity ?? world.Configuration.Rules.StartingBandCuriosity,
    });
}
