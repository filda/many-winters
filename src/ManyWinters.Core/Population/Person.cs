using System.Diagnostics.CodeAnalysis;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Population;

public sealed class Person
{
    // Where every family line ends. Parents are always real Person objects (see Mother), so
    // someone with no recorded ancestry still has to point at *somebody* - this is that
    // somebody: the empty id (no entity ever draws it - see EntityId), long dead, never on any
    // map, and its own mother and father so the chain terminates without a null anywhere along
    // it. Nobody can ever click it and discover the loop, because nothing ever puts it into a
    // world.
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

        // Arbitrary, and never read: this one is dead, is nobody's parent in the sense that
        // matters (see Kinship), and never appears in a world. Drawn from the id rather than
        // written as a literal so it is at least not a claim about anything.
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

    // Never null: a person whose parents nobody remembers has Unknown here, and one whose
    // parents died before the story began has a forebear (WorldState.Forebears) - a full
    // Person with a name and a life of its own that simply isn't on the map. Either way a
    // grave can always write "child of X and Y" without asking first (see BuryCommand).
    public required Person Mother { get; init; }

    public required Person Father { get; init; }

    // Required, like Mother and Father and for the same reason: there is no such thing as a
    // person without one, so nobody gets to leave it to chance by accident. A caller that
    // genuinely does not care says so out loud with SexOf below, rather than this quietly
    // drawing for them - which it used to, and which made every test person's sex a fresh coin
    // flip per run.
    //
    // Saved rather than re-derived from the id on load (see PersonSaveData): a sex somebody
    // chose would otherwise be replaced by whatever the id happens to say on the next reload.
    public required Sex Sex { get; init; }

    public Needs Needs { get; } = new();

    public Skills Skills { get; } = new();

    public HashSet<TechniqueId> KnownTechniques { get; } = new();

    public Inventory Inventory { get; } = new();

    public PersonTaskQueue Tasks { get; } = new();

    // A plausible sex for someone nobody has an opinion about, drawn from their own id the way
    // every other per-entity variation in this game is (EntityVisualVariation's tint and
    // scale, an idle wander, a casual-teaching roll) - so it is stable across a reload and
    // spread by SeedHash first, because ids that sit close together must not come out alike.
    //
    // This is what a caller with no stake in the answer reaches for (SpawnPersonCommand and
    // the test spawn helper both do), which is deliberately a thing you have to ask for.
    public static Sex SexOf(PersonId id) =>
        (SeedHash.Avalanche(unchecked((uint)id.Seed)) & 1) == 0 ? Sex.Female : Sex.Male;

    // Ticks (WorldState.Clock.CurrentTick) before which WorldState.Advance won't drop this
    // person into an IdleTask even with an empty queue - lets the presentation layer (the
    // currently-selected person, say) buy someone a few ticks of standing still rather than
    // wandering off between manual actions. 0 by default: nobody's exempt unless granted.
    public long IdleGraceUntilTick { get; set; }
}
