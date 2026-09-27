using ManyWinters.Core.World;

namespace ManyWinters.Core.Population;

// The second Creature (docs/todo/fauna-plan.md, phase 1a): no name, no father, no beliefs - just
// what species it is, the ground it wanders (HomeRange) and, for a young one, its mother.
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
    // unlike a Person, who has one once born or spawned into a real band (see Person.Home).
    // Manually backed, rather than an auto-property, because it must supply only the getter -
    // Creature.Home's init accessor exists for Person's benefit; an Animal's home is fixed for
    // life at the constructor, exactly as before this step.
    public override HomeRange Home => _home;

    // Unlike Person.Mother (required, never null - see Person.Mother), an animal spawned as an
    // adult (MapLoader's starting herds, phase 1a) has none to point at.
    public Animal? Mother { get; init; }

    public override Creature? NursingMother => Mother;

    // Set by WorldState.BreedAnimals the tick she conceives, cleared again the tick she gives
    // birth (phase 1b, "mnozeni"). Null for a male and for a female not currently carrying.
    public long? PregnantSinceTick { get; set; }
}
