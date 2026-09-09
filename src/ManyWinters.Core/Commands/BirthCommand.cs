using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// A child born to two people standing together - the first thing in the game that adds to the
// population rather than moving what is already there around (SpawnPersonCommand is the
// player conjuring someone out of nothing; MapLoader is the world starting).
//
// What a newborn is *not* is the point. No skills, no techniques, nothing in its pack:
// knowledge in this game is never inherited, only taught (see TeachCommand), so a child born
// to two expert foragers starts exactly as ignorant as one born to anybody else, and gets
// what it gets by staying at its mother's side long enough to pick it up.
public sealed record BirthCommand(string Name, Person Mother, Person Father) : ICommand
{
    public void Execute(WorldState world)
    {
        if (!CanBearAChild(world))
        {
            return;
        }

        // Born where its mother is, not at some free spot found for it: a newborn that has to
        // be placed somewhere is already a newborn that has been put down and walked away
        // from. WorldState.ResolveCollisions untangles the overlap on the same tick.
        world.AddPerson(new Person
        {
            Name = Name,
            Position = Mother.Position,
            BirthTick = world.Clock.CurrentTick,
            Mother = Mother,
            Father = Father,
        });
    }

    private bool CanBearAChild(WorldState world) =>
        Mother.IsAlive
        && Father.IsAlive
        // Not the same person twice. Nothing else here would catch it: someone is trivially
        // alive, adult and within reach of themselves.
        && !ReferenceEquals(Mother, Father)
        && world.IsOldEnoughForChildren(Mother)
        && world.IsOldEnoughForChildren(Father)
        && world.IsWithinReach(Mother.Position, Father.Position)
        // One at a time. A mother already nursing cannot feed a second newborn (see
        // WorldState.NursingInfantOf), and a child that cannot be fed is a child that starves
        // within the winter.
        && world.NursingInfantOf(Mother) is null;
}
