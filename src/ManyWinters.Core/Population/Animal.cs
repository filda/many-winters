using ManyWinters.Core.World;

namespace ManyWinters.Core.Population;

// A Creature with no name, no father, no beliefs - just what species it is, the ground it
// wanders (HomeRange) and, for a young one, its mother.
public sealed class Animal : Creature
{
    private readonly HomeRange _home;

    public Animal(SpeciesId species, HomeRange home)
    {
        Species = species;
        _home = home;
    }

    public override SpeciesId Species { get; }

    // Covariant override of Creature.Home (Person leaves it null): every Animal always has one,
    // unlike a Person, who gets one only once born or placed in a band. Manually backed, not an
    // auto-property, because only the getter is needed here - Creature.Home's init accessor
    // exists for Person's benefit; an Animal's home is fixed at construction.
    public override HomeRange Home => _home;

    // Unlike Person.Mother, which is required and never null, an animal spawned as an adult
    // starting member of a herd has none to point at.
    public Animal? Mother { get; init; }

    public override Creature? NursingMother => Mother;

    // Set the tick she conceives, cleared again the tick she gives birth. Null for a male and for
    // a female not currently carrying.
    public long? PregnantSinceTick { get; set; }
}
