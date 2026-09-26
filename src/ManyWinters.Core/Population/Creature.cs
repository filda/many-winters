using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Population;

// What every living thing has, whether it grows up to have a name or not (docs/todo/fauna-plan.md,
// step 0b). Person is the only descendant today; an animal will be the next one.
public abstract class Creature
{
    // Drawn here, not handed out by a world - see EntityId.
    public CreatureId Id { get; init; } = CreatureId.New();

    public Position Position { get; set; }

    public bool IsAlive { get; set; } = true;

    public required long BirthTick { get; init; }

    public long? DeathTick { get; set; }

    public DeathCause? CauseOfDeath { get; set; }

    // Required like Mother and Father: nobody leaves it to chance by accident. A caller with no
    // opinion says so with SexOf rather than this drawing quietly, which would make every test
    // person's sex a coin flip per run. Saved rather than re-derived from the id (PersonSaveData),
    // or a chosen sex would be replaced by the id's draw on reload.
    public required Sex Sex { get; init; }

    // The hunger this creature dies at (WorldState.Advance checks Needs.Hunger against it). Drawn
    // off their own id by SimulationRules.MaxHungerFor when created inside a world, so two people
    // born the same tick don't run out together; the default is for a person built outside any
    // world. Not saved: unlike Sex it is only ever the draw, and the draw comes back off the id.
    public float MaxHunger { get; init; } = SimulationRules.Default.MaxHunger;

    public Needs Needs { get; } = new();

    public Skills Skills { get; } = new();

    public HashSet<TechniqueId> KnownTechniques { get; } = new();

    public Inventory Inventory { get; } = new();

    public CreatureTaskQueue Tasks { get; } = new();

    // A plausible sex for someone nobody has an opinion about, drawn from their id like every
    // other per-entity variation, so it survives a reload; spread by SeedHash first because
    // close ids must not come out alike. What a caller with no stake reaches for
    // (SpawnPersonCommand) - deliberately something you have to ask for.
    public static Sex SexOf(CreatureId id) =>
        (SeedHash.Avalanche(unchecked((uint)id.Seed)) & 1) == 0 ? Sex.Female : Sex.Male;

    // Ticks (WorldState.Clock.CurrentTick) before which WorldState.Advance won't drop this creature
    // into an IdleTask despite an empty queue - lets the presentation layer buy the selected
    // person a few ticks of standing still between manual actions. 0: no exemption.
    public long IdleGraceUntilTick { get; set; }

    // The nursing/following parent as seen by the shared simulation (WorldState.IsNursedBy,
    // DecideIdleTask's infant-follows-mother rule). A Person always has one, even if it is
    // Person.Unknown; an animal may have none.
    public abstract Creature? NursingMother { get; }

    // What this creature is (docs/todo/fauna-plan.md, step 0c: "clovek je taky druh" - a human is
    // a species too). Looked up in WorldConfiguration.SpeciesCatalog for the age bands and
    // lifespan that used to be hardcoded constants (WorldState.LifeCycleOf).
    public abstract SpeciesId Species { get; }

    // The shared ground this creature wanders around (WorldState.DecideIdleTask), if it has one.
    // Null for a Person today - people still wander from wherever they stand (step 1b moves the
    // starting band onto a shared camp anchor). An Animal always has one (see Animal.Home),
    // exposed here as a covariant override the same way NursingMother is, so WorldState never
    // has to ask "is this an Animal" to find it (docs/todo/fauna-plan.md, "Co je stado konkretne").
    public virtual HomeRange? Home => null;
}
