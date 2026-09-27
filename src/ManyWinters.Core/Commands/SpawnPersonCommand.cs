using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// Mother and Father are required; a caller with nobody to name passes Person.Unknown. The id is
// normally the person's to draw - only a creator that must produce the same world twice
// (MapLoader) names one.
public sealed record SpawnPersonCommand(
    CreatureId Id,
    string Name,
    Position Position,
    Person Mother,
    Person Father,
    long InitialAgeTicks = 0,
    // Null lets the id decide. MapLoader sets it: its family table has already settled who bore
    // whom.
    Sex? Sex = null,
    // Null takes the player band's rate from the rules; an NPC band passes its own.
    float? Curiosity = null,
    // Null leaves the new person with no home - a caller with an opinion passes one:
    // MapLoader.LoadDefault hands every starting/successor band member the same camp HomeRange,
    // Main.OnSpawnButtonPressed borrows the nearest living person's.
    HomeRange? Home = null) : ICommand
{
    public SpawnPersonCommand(string name, Position position, Person mother, Person father, long initialAgeTicks = 0, HomeRange? home = null)
        : this(CreatureId.New(), name, position, mother, father, initialAgeTicks, Home: home)
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
