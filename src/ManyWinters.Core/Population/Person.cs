using System.Diagnostics.CodeAnalysis;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Population;

public sealed class Person : Creature
{
    // A well-known id, declared once rather than drawn or configured per instance. Every Person
    // is this species.
    public static readonly SpeciesId HumanSpecies = new("human");

    public Person()
    {
    }

    // Stryker disable once Block: as the fields below - this initializer runs once per process, never for the test that checks it
    [SetsRequiredMembers]
    private Person(string unknownRootName)
    {
        Id = new CreatureId(Guid.Empty);
        Name = unknownRootName;
        BirthTick = 0;

        // Stryker disable once Boolean: a test asserts Unknown is dead and buried, but a static initializer runs once per process - never for that test
        IsAlive = false;
        // Stryker disable once Boolean: as IsAlive above
        IsBuried = true;
        Mother = this;
        Father = this;
        Home = HomeRange.Unknown;

        // Never read: dead, nobody's parent, never in a world. Drawn from the id
        // rather than written as a literal so it is not a claim about anything.
        Sex = SexOf(Id);
    }

    // Where every family line ends. Parents are always real Person objects, so someone with no
    // recorded ancestry points here: the empty id (no entity ever draws it), long dead, never in
    // any world, and its own mother and father so the chain terminates without a null.
    public static Person Unknown { get; } = new(unknownRootName: "Unknown");

    public required string Name { get; init; }

    public bool IsBuried { get; set; }

    // Never null: unremembered parents are Unknown, parents who died before the story began are
    // forebears, so a grave can always write "child of X and Y".
    public required Person Mother { get; init; }

    public required Person Father { get; init; }

    // How readily this person works a thing out for themselves, as a multiplier on the idle
    // discovery roll. One is the rate the shipped band learns at. It sits on the person rather
    // than in the rules because it is meant to differ between bands: an NPC tribe that should
    // develop more slowly than the player's own is the same world with a lower number here, not a
    // second set of rules. Uniform within a band today; docs/materials-and-crafting-architecture.md
    // section 7 lists per-person variation as an option nobody has decided on.
    public float Curiosity { get; init; } = SimulationRules.Default.StartingBandCuriosity;

    // What this person takes the substances they have handled to be. Per person, like Skills and
    // KnownTechniques: two people who have handled different things understand different things,
    // and what nobody alive believes is lost with them.
    public Beliefs Beliefs { get; } = new();

    // The base class's abstract hook, satisfied with this person's own typed, never-null Mother.
    // A covariant return - C# allows narrowing an override's return type - so callers can read
    // NursingMother uniformly while a Person's own callers keep using Mother directly.
    public override Person NursingMother => Mother;

    // The band's camp: inherited at birth, handed out by whoever founds the band.
    public override required HomeRange Home { get; init; }

    public override SpeciesId Species => HumanSpecies;
}
