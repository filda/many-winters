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

        // Drawn here rather than left to the Person's own default (see EntityId), only because
        // the child's sex is drawn from its id and so the id has to exist first. Nobody knows
        // a newborn's sex in advance, which is exactly what Person.SexOf is for.
        var id = PersonId.New();

        // Born where its mother is, not at some free spot found for it: a newborn that has to
        // be placed somewhere is already a newborn that has been put down and walked away
        // from. WorldState.ResolveCollisions untangles the overlap on the same tick.
        var child = new Person
        {
            Id = id,
            Name = Name,
            Sex = Person.SexOf(id),
            Position = Mother.Position,
            BirthTick = world.Clock.CurrentTick,
            Mother = Mother,
            Father = Father,
        };

        world.AddPerson(child);

        // A child is close to its parents from the first day rather than having to earn it by
        // standing near them (docs/todo/todo.md, "u potomku automaticky vyšší"). Only the
        // parents: what a newborn is to its siblings is a question this doesn't answer yet.
        var startingAffection = world.Configuration.Rules.StartingAffectionWithParents;
        world.Affections.Set(child.Id, Mother.Id, startingAffection);
        world.Affections.Set(child.Id, Father.Id, startingAffection);
    }

    // Everything that decides whether a birth is possible at all lives here rather than in
    // whoever asked for one, so the player's button and WorldState's own autonomous pass can
    // never disagree about it. What the two of them do differ on is *motivation* - the button
    // is the player deciding, the pass waits for the pair to be fond enough of each other.
    private bool CanBearAChild(WorldState world) =>
        Mother.IsAlive
        && Father.IsAlive
        // Not the same person twice. Nothing else here would catch it: someone is trivially
        // alive, adult and within reach of themselves.
        && !ReferenceEquals(Mother, Father)
        && Mother.Sex == Sex.Female
        && Father.Sex == Sex.Male
        && !Kinship.AreCloseKin(Mother, Father)
        && world.IsOldEnoughForChildren(Mother)
        && world.IsOldEnoughForChildren(Father)
        && world.IsWithinReach(Mother.Position, Father.Position)
        // One at a time. A mother already nursing cannot feed a second newborn (see
        // WorldState.NursingInfantOf), and a child that cannot be fed is a child that starves
        // within the winter.
        && world.NursingInfantOf(Mother) is null;
}
