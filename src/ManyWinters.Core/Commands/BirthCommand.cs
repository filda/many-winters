using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// A child born to two people standing together. A newborn has no skills, techniques or items:
// knowledge is never inherited, only taught (see TeachCommand), so a child of two expert
// foragers starts as ignorant as any other.
public sealed record BirthCommand(string Name, Person Mother, Person Father) : ICommand
{
    public void Execute(WorldState world)
    {
        if (!CanBearAChild(world))
        {
            return;
        }

        // Drawn before the Person exists because the sex is drawn from the id (see Person.SexOf).
        var id = PersonId.New();

        // Born where its mother is; WorldState.ResolveCollisions untangles the overlap the same
        // tick.
        var child = new Person
        {
            Id = id,
            Name = Name,
            Sex = Person.SexOf(id),
            Position = Mother.Position,
            BirthTick = world.Clock.CurrentTick,
            Mother = Mother,
            Father = Father,
            MaxHunger = world.Configuration.Rules.MaxHungerFor(id),
        };

        world.AddPerson(child);

        // Close to its parents from the first day rather than earning it by standing near them.
        // Only the parents: siblings are not covered yet (docs/todo/todo.md).
        var startingAffection = world.Configuration.Rules.StartingAffectionWithParents;
        world.Affections.Set(child.Id, Mother.Id, startingAffection);
        world.Affections.Set(child.Id, Father.Id, startingAffection);
    }

    // Every precondition lives here, so the player's button and WorldState's autonomous pass
    // can never disagree; they differ only in motivation.
    private bool CanBearAChild(WorldState world) =>
        Mother.IsAlive
        && Father.IsAlive
        // Nothing else here would catch the same person twice.
        && !ReferenceEquals(Mother, Father)
        && Mother.Sex == Sex.Female
        && Father.Sex == Sex.Male
        && !Kinship.AreCloseKin(Mother, Father)
        && world.IsOldEnoughForChildren(Mother)
        && world.IsOldEnoughForChildren(Father)
        && world.IsWithinReach(Mother.Position, Father.Position)
        // One at a time: a mother already nursing cannot feed a second newborn
        // (see WorldState.NursingInfantOf).
        && world.NursingInfantOf(Mother) is null;
}
