using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// SpawnPersonCommand's counterpart for the second kind of Creature (docs/todo/fauna-plan.md,
// phase 1a). The id is normally the animal's own to draw (see EntityId) - only a creator that
// must produce the same world twice (MapLoader) names one.
public sealed record SpawnAnimalCommand(
    CreatureId Id,
    SpeciesId Species,
    Position Position,
    HomeRange Home,
    Sex Sex,
    long BirthTick,
    // Null for one spawned as an adult (MapLoader's starting herds); set once a fawn is born
    // (phase 1b).
    Animal? Mother = null) : ICommand
{
    // World-building, not a player action, exactly like SpawnPersonCommand: whoever calls this
    // is creating the world rather than acting inside it, so there is nothing to refuse.
    public ActionBlocker Blocker(WorldState world) => ActionBlocker.None;

    public void Execute(WorldState world)
    {
        var animal = new Animal(Species, Home)
        {
            Id = Id,
            Position = Position,
            BirthTick = BirthTick,
            Sex = Sex,
            MaxHunger = world.Configuration.Rules.MaxHungerFor(Id),
            Mother = Mother,
        };

        // "Uz maji neco naucemo" (docs/todo/todo.md): an animal is autonomous from the moment it
        // exists, rather than starting as ignorant as a newborn person.
        foreach (var technique in world.Configuration.SpeciesCatalog.Get(Species).InnateTechniques)
        {
            animal.KnownTechniques.Add(technique);
        }

        world.AddAnimal(animal);
    }
}
