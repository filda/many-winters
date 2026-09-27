using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Population;

public sealed record SpeciesDefinition(
    SpeciesId Id,
    string DisplayName,
    LifeCycle LifeCycle,
    // What this species can get anything out of, and how well - a wolf can eat a pear but it
    // will not keep it going (0 digestibility), and grass feeds a deer and not a person (not in
    // the list at all). Empty for a species with no diet defined at all, not for one that eats
    // nothing (docs/todo/fauna-plan.md, step 0d: diets are per species with per-material
    // digestibility).
    IReadOnlyList<SpeciesDefinition.DietEntry>? Diet = null,
    // Techniques a creature of this species already has the moment it is spawned
    // (SpawnAnimalCommand) - the todo's "uz maji neco naucemo, takze funguji autonomne". Empty
    // for a human: nobody, including the shipped starting band, is born knowing how to eat or
    // forage (see SkillDefinition.BaseTechnique) - that would defeat the point of a band that
    // starts ignorant (docs/todo/fauna-plan.md, "Teaching is the game").
    IReadOnlyList<TechniqueId>? InnateTechniques = null,
    // Whether this species pockets anything at all (WorldState.MaxCarryWeightFor returns 0 when
    // false). A human can; an animal cannot, which is what makes "eats when hungry, otherwise
    // wanders" fall out of GatherCommand.CanTakeAnythingFrom for free rather than needing its own
    // rule (docs/todo/fauna-plan.md, "Pastva").
    bool CanCarry = true,
    // Half-width of this species' footprint for collision resolution, in metres
    // (WorldState.ResolveCollisions) - what used to be the single, human-only
    // SimulationRules.PersonCollisionRadius before every species could need its own.
    float CollisionRadius = 0.35f,
    // Null for a species with no herd - a human, today. Present for a species that spawns as a
    // group sharing one HomeRange (MapLoader.LoadDefault's starting deer herds).
    SpeciesDefinition.HerdDefinition? Herd = null,
    // Null for a species that does not breed through this rule - a human, today (people breed
    // through Affections/BirthCommand instead). Present for a species whose females conceive on
    // a per-tick roll (docs/todo/fauna-plan.md, phase 1b, "mnozeni").
    SpeciesDefinition.BreedingDefinition? Breeding = null,
    // Multiplies SimulationRules.HungerPerTick for every creature of this species
    // (WorldState.Advance) - the shipped map's winter reserve (docs/todo/fauna-plan.md, phase 1's
    // "Otevřené ladění": a herd that ran on the human rate halved every winter). 1 for a human, so
    // nothing about a person changes; below 1 for a species that needs to run leaner through a
    // lean season.
    float HungerPerTickMultiplier = 1f,
    // Null for a species that never flees anyone - a human, today. Present for a species that
    // breaks off whatever it is doing the moment a living person comes within FleeDistance
    // (WorldState.DecideIdleTask, FleeTask) - docs/todo/fauna-plan.md, "Útěk dřív než lov".
    SpeciesDefinition.FleeDefinition? Flee = null)
{
    public sealed record DietEntry(MaterialId Material, float Digestibility);

    // Group size and the shared HomeRange it spawns with - MinSize/MaxSize (MapLoader draws a
    // herd's actual size from this range), HomeRadius (IdleTask's wander radius around the
    // shared anchor) and DriftMetresPerSeason (HomeRange.Advance's per-season move).
    public sealed record HerdDefinition(int MinSize, int MaxSize, float HomeRadius, float DriftMetresPerSeason);

    // The species' own mating rule (WorldState.BreedAnimals): the climate she must be in to
    // conceive (keyed on Climate, never a Season - see SeasonParameters and
    // ResourceDefinition.ClimateYields), how long she then carries the pregnancy, the per-tick
    // chance an eligible female conceives, and how well fed (Needs.Hunger below this) she must
    // be to count as eligible at all.
    public sealed record BreedingDefinition(Climate Climate, long GestationTicks, float ConceptionChancePerTick, float SatietyHungerBelow);

    // A species' own flight rule (WorldState.DecideIdleTask, FleeTask): break off and move
    // directly away from the nearest living person once one is closer than FleeDistance, until
    // the gap reaches SafeDistance, at SpeedPerTick.
    public sealed record FleeDefinition(float FleeDistance, float SafeDistance, float SpeedPerTick);

    // C# does not allow a collection-expression default on the primary constructor parameters
    // above, so the empty-collection normalization happens here instead (see
    // ResourceDefinition.ClimateYields).
    public IReadOnlyList<DietEntry> Diet { get; } = Diet ?? [];

    public IReadOnlyList<TechniqueId> InnateTechniques { get; } = InnateTechniques ?? [];

    // How well this species digests the given material - 0 (cannot eat it at all) for anything
    // not in the diet.
    public float DigestibilityOf(MaterialId material)
    {
        foreach (var entry in Diet)
        {
            if (entry.Material == material)
            {
                return entry.Digestibility;
            }
        }

        return 0f;
    }
}
