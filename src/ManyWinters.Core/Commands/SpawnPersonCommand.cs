using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Mother and Father are required; a caller with nobody to name passes the sentinel unknown
// person. The id is normally the person's to draw - only a creator that must produce the same
// world twice (MapLoader) names one.
public sealed record SpawnPersonCommand(
    CreatureId Id,
    string Name,
    Position Position,
    Person Mother,
    Person Father,
    // The band's camp this person joins.
    HomeRange Home,
    long InitialAgeTicks = 0,
    // Null lets the id decide. MapLoader sets it: its family table has already settled who bore
    // whom.
    Sex? Sex = null,
    // Null takes the player band's rate from the rules; an NPC band passes its own.
    float? Curiosity = null) : ICommand
{
    public SpawnPersonCommand(string name, Position position, Person mother, Person father, HomeRange home, long initialAgeTicks = 0)
        : this(CreatureId.New(), name, position, mother, father, home, initialAgeTicks)
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
        Sex = Sex ?? Creature.SexOf(Id),
        MaxHunger = world.Configuration.Rules.MaxHungerFor(Id),
        Curiosity = Curiosity ?? world.Configuration.Rules.StartingBandCuriosity,
        Home = Home,
    });
}
