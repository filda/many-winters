using System.Diagnostics.CodeAnalysis;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Population;

public sealed class Person
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
        Id = new PersonId(Guid.Empty);
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

    // Drawn here, not handed out by a world - see EntityId.
    public PersonId Id { get; init; } = PersonId.New();

    public required string Name { get; init; }

    public Position Position { get; set; }

    public bool IsAlive { get; set; } = true;

    public required long BirthTick { get; init; }

    public long? DeathTick { get; set; }

    public DeathCause? CauseOfDeath { get; set; }

    public bool IsBuried { get; set; }

    // Never null: unremembered parents are Unknown, parents who died before the story began are
    // forebears (WorldState.Forebears), so a grave can always write "child of X and Y"
    // (BuryCommand).
    public required Person Mother { get; init; }

    public required Person Father { get; init; }

    // Required like Mother and Father: nobody leaves it to chance by accident. A caller with no
    // opinion says so with SexOf rather than this drawing quietly, which would make every test
    // person's sex a coin flip per run. Saved rather than re-derived from the id (PersonSaveData),
    // or a chosen sex would be replaced by the id's draw on reload.
    public required Sex Sex { get; init; }

    // The hunger this person dies at (WorldState.Advance checks Needs.Hunger against it). Drawn
    // off their own id by SimulationRules.MaxHungerFor when created inside a world, so two people
    // born the same tick don't run out together; the default is for a person built outside any
    // world. Not saved: unlike Sex it is only ever the draw, and the draw comes back off the id.
    public float MaxHunger { get; init; } = SimulationRules.Default.MaxHunger;

    // How readily this person works a thing out for themselves, as a multiplier on the idle
    // discovery roll (see WorldState.DiscoverByFiddling). One is the rate the shipped band
    // learns at. It sits on the person rather than in the rules because it is meant to differ
    // between bands: an NPC tribe that should develop more slowly than the player's own is the
    // same world with a lower number here, not a second set of rules (docs/todo/todo.md, NPC
    // tribes). Uniform within a band today; section 7 of
    // docs/materials-and-crafting-architecture.md notes per-person variation as an option
    // nobody has decided on.
    public float Curiosity { get; init; } = SimulationRules.Default.StartingBandCuriosity;

    public Needs Needs { get; } = new();

    public Skills Skills { get; } = new();

    public HashSet<TechniqueId> KnownTechniques { get; } = new();

    public Inventory Inventory { get; } = new();

    public PersonTaskQueue Tasks { get; } = new();

    // A plausible sex for someone nobody has an opinion about, drawn from their id like every
    // other per-entity variation, so it survives a reload; spread by SeedHash first because
    // close ids must not come out alike. What a caller with no stake reaches for
    // (SpawnPersonCommand) - deliberately something you have to ask for.
    public static Sex SexOf(PersonId id) =>
        (SeedHash.Avalanche(unchecked((uint)id.Seed)) & 1) == 0 ? Sex.Female : Sex.Male;

    // Ticks (WorldState.Clock.CurrentTick) before which WorldState.Advance won't drop this person
    // into an IdleTask despite an empty queue - lets the presentation layer buy the selected
    // person a few ticks of standing still between manual actions. 0: no exemption.
    public long IdleGraceUntilTick { get; set; }
}
