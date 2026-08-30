using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Population;

public sealed class Person
{
    public required PersonId Id { get; init; }

    public required string Name { get; init; }

    public Position Position { get; set; }

    public bool IsAlive { get; set; } = true;

    public required long BirthTick { get; init; }

    public long? DeathTick { get; set; }

    public DeathCause? CauseOfDeath { get; set; }

    public bool IsBuried { get; set; }

    public PersonId? MotherId { get; init; }

    public PersonId? FatherId { get; init; }

    public Needs Needs { get; init; } = new();

    public Skills Skills { get; init; } = new();

    public HashSet<TechniqueId> KnownTechniques { get; init; } = new();

    public Inventory Inventory { get; init; } = new();

    public PersonTaskQueue Tasks { get; init; } = new();

    // Ticks (WorldState.Clock.CurrentTick) before which WorldState.Advance won't drop this
    // person into an IdleTask even with an empty queue - lets the presentation layer (the
    // currently-selected person, say) buy someone a few ticks of standing still rather than
    // wandering off between manual actions. 0 by default: nobody's exempt unless granted.
    public long IdleGraceUntilTick { get; set; }
}
