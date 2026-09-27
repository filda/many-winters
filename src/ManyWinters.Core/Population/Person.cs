using System.Diagnostics.CodeAnalysis;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Population;

public sealed class Person : Creature
{
    // Where every family line ends. Parents are always real Person objects (see Mother), so
    // someone with no recorded ancestry points here: the empty id (no entity ever draws it - see
    // EntityId), long dead, never in any world, and its own mother and father so the chain
    // terminates without a null.
    public static Person Unknown { get; } = new(unknownRootName: "Unknown");

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

        // Stryker disable once Boolean: PersonTests.UnknownIsLongDeadAndBuried asserts this, but a static initializer runs once per process - never for that test
        IsAlive = false;
        // Stryker disable once Boolean: as IsAlive above
        IsBuried = true;
        Mother = this;
        Father = this;

        // Never read: dead, nobody's parent (see Kinship), never in a world. Drawn from the id
        // rather than written as a literal so it is not a claim about anything.
        Sex = SexOf(Id);
    }

    public required string Name { get; init; }

    public bool IsBuried { get; set; }

    // Never null: unremembered parents are Unknown, parents who died before the story began are
    // forebears (WorldState.Forebears), so a grave can always write "child of X and Y"
    // (BuryCommand).
    public required Person Mother { get; init; }

    public required Person Father { get; init; }

    // How readily this person works a thing out for themselves, as a multiplier on the idle
    // discovery roll (see WorldState.DiscoverByFiddling). One is the rate the shipped band
    // learns at. It sits on the person rather than in the rules because it is meant to differ
    // between bands: an NPC tribe that should develop more slowly than the player's own is the
    // same world with a lower number here, not a second set of rules (docs/todo/todo.md, NPC
    // tribes). Uniform within a band today; section 7 of
    // docs/materials-and-crafting-architecture.md notes per-person variation as an option
    // nobody has decided on.
    public float Curiosity { get; init; } = SimulationRules.Default.StartingBandCuriosity;

    // What this person takes the substances they have handled to be (see Beliefs). Per person,
    // like Skills and KnownTechniques: two people who have handled different things understand
    // different things, and what nobody alive believes is lost with them.
    public Beliefs Beliefs { get; } = new();

    // The base class's abstract hook, satisfied with this person's own typed, never-null Mother
    // (docs/todo/fauna-plan.md, step 0b). A covariant return - C# allows narrowing an override's
    // return type - so WorldState's nursing/following code can read Creature.NursingMother
    // uniformly while a Person's own callers keep using Person.Mother directly.
    public override Person NursingMother => Mother;

    // Covariant override of Creature.Home (docs/todo/fauna-plan.md, step 1b, "Osadnici na
    // sdilenou kotvu tabora"): unlike Animal.Home, settable and nullable, because a person built
    // outside any map (most tests) and Person.Unknown have none and behave exactly as before -
    // IdleTask anchors wherever they stand and the idle food search centres on themselves
    // (WorldState.DecideIdleTask). Every member of a real band gets one (MapLoader.LoadDefault),
    // inherited at birth (BirthCommand) and, for a debug-spawned person, borrowed from the
    // nearest living person (Main.OnSpawnButtonPressed).
    public override HomeRange? Home { get; init; }

    // A well-known id, declared once rather than drawn or configured per instance - the same
    // pattern as EatCommand.Skill (docs/todo/fauna-plan.md, step 0c). Every Person is this
    // species; an animal's own kind is the point of the step that introduces it.
    public static readonly SpeciesId HumanSpecies = new("human");

    public override SpeciesId Species => HumanSpecies;
}
