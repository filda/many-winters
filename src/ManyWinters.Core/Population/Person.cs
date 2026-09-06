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
    // somebody: id 0 (never handed out by any world), long dead, never on any map, and its own
    // mother and father so the chain terminates without a null anywhere along it. Nobody can
    // ever click it and discover the loop, because nothing ever puts it into a world.
    public static Person Unknown { get; } = new(unknownRootName: "Unknown");

    public Person()
    {
    }

    [SetsRequiredMembers]
    private Person(string unknownRootName)
    {
        Id = new PersonId(0);
        Name = unknownRootName;
        BirthTick = 0;
        IsAlive = false;
        IsBuried = true;
        Mother = this;
        Father = this;
    }

    public required PersonId Id { get; init; }

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

    public Needs Needs { get; } = new();

    public Skills Skills { get; } = new();

    public HashSet<TechniqueId> KnownTechniques { get; } = new();

    public Inventory Inventory { get; } = new();

    public PersonTaskQueue Tasks { get; } = new();

    // Ticks (WorldState.Clock.CurrentTick) before which WorldState.Advance won't drop this
    // person into an IdleTask even with an empty queue - lets the presentation layer (the
    // currently-selected person, say) buy someone a few ticks of standing still rather than
    // wandering off between manual actions. 0 by default: nobody's exempt unless granted.
    public long IdleGraceUntilTick { get; set; }
}
