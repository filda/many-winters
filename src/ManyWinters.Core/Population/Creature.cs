using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Population;

// What every living thing has, whether it grows up to have a name or not.
public abstract class Creature
{
    // Drawn here, not handed out by a world, unlike an EntityId.
    public CreatureId Id { get; init; } = CreatureId.New();

    public Position Position { get; set; }

    public bool IsAlive { get; set; } = true;

    public required long BirthTick { get; init; }

    public long? DeathTick { get; set; }

    public DeathCause? CauseOfDeath { get; set; }

    // Required like Mother and Father: nobody leaves it to chance by accident. A caller with no
    // opinion says so with SexOf rather than this drawing quietly, which would make every test
    // person's sex a coin flip per run. Saved rather than re-derived from the id, or a chosen sex
    // would be replaced by the id's draw on reload.
    public required Sex Sex { get; init; }

    // The hunger this creature dies at. Drawn off their own id when created inside a world, so
    // two people born the same tick don't run out together; the default is for a person built
    // outside any world. Not saved: unlike Sex it is only ever the draw, and the draw comes back
    // off the id.
    public float MaxHunger { get; init; } = SimulationRules.Default.MaxHunger;

    public Needs Needs { get; } = new();

    public Skills Skills { get; } = new();

    public HashSet<TechniqueId> KnownTechniques { get; } = new();

    public Inventory Inventory { get; } = new();

    public CreatureTaskQueue Tasks { get; } = new();

    // A plausible sex for someone nobody has an opinion about, drawn from their id like every
    // other per-entity variation, so it survives a reload; spread first so close ids don't come
    // out alike. What a caller with no stake reaches for - deliberately something you have to
    // ask for.
    public static Sex SexOf(CreatureId id) =>
        (SeedHash.Avalanche(unchecked((uint)id.Seed)) & 1) == 0 ? Sex.Female : Sex.Male;

    // Ticks before which the simulation won't drop this creature into idling despite an empty
    // queue - lets the presentation layer buy the selected person a few ticks of standing still
    // between manual actions. 0: no exemption.
    public long IdleGraceUntilTick { get; set; }

    // The nursing/following parent as seen by the shared simulation's infant-follows-mother rule.
    // A Person always has one, even if it is the sentinel unknown person; an animal may have none.
    public abstract Creature? NursingMother { get; }

    // What this creature is - a human is a species too. Looked up in the species catalog for the
    // age bands and lifespan.
    public abstract SpeciesId Species { get; }

    // The shared ground this creature wanders around, if it has one. An Animal always has one; a
    // Person has one once born or spawned into a real band and null otherwise - a person built
    // outside any map, or the sentinel unknown person, wanders from wherever they stand instead.
    // Exposed here as a covariant override the same way NursingMother is, so callers never have
    // to ask "is this an Animal" to find it.
    public virtual HomeRange? Home { get; init; }
}
