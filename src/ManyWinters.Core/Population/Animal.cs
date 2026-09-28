using ManyWinters.Core.World;

namespace ManyWinters.Core.Population;

// A Creature with no name, no father, no beliefs - just what species it is, the ground it
// wanders (HomeRange) and, for a young one, its mother.
public sealed class Animal : Creature
{
    public Animal(SpeciesId species, HomeRange home)
    {
        Species = species;
        Home = home;
    }

    public override SpeciesId Species { get; }

    public override HomeRange Home { get; init; }

    // Unlike a person's Mother, which is required and never null, an animal spawned as an adult
    // starting member of a herd has none to point at.
    public Animal? Mother { get; init; }

    public override Creature? NursingMother => Mother;

    // Set the tick she conceives, cleared again the tick she gives birth. Null for a male and for
    // a female not currently carrying.
    public long? PregnantSinceTick { get; set; }
}
