using ManyWinters.Core.Items;
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
    // nothing.
    IReadOnlyList<SpeciesDefinition.DietEntry>? Diet = null,
    // Techniques a creature of this species already has the moment it is spawned, so it
    // functions autonomously from the start. Empty for a human: nobody, including the shipped
    // starting band, is born knowing how to eat or forage - that would defeat the point of a
    // band that starts ignorant.
    IReadOnlyList<TechniqueId>? InnateTechniques = null,
    // Whether this species pockets anything at all. A human can; an animal cannot, which is what
    // makes "eats when hungry, otherwise wanders" fall out for free rather than needing its own
    // rule.
    bool CanCarry = true,
    // Null for a species with no herd - a human, today. Present for a species that spawns as a
    // group sharing one HomeRange, such as a starting deer herd.
    SpeciesDefinition.HerdDefinition? Herd = null,
    // Null for a species that does not breed through this rule - a human, today, whose breeding
    // works through a separate mechanism. Present for a species whose females conceive on a
    // per-tick roll.
    SpeciesDefinition.BreedingDefinition? Breeding = null,
    // Null for a species that never flees anyone - a human, today. Present for a species that
    // breaks off whatever it is doing the moment a living person comes within FleeDistance.
    SpeciesDefinition.FleeDefinition? Flee = null,
    // What a dead creature of this species leaves behind, put into its Inventory once at the
    // moment it dies, whatever the cause. Empty for a human: people are not butchered, and
    // taking a dead person's possessions works through a separate command instead.
    IReadOnlyList<SpeciesDefinition.CarcassYield>? Carcass = null)
{
    public sealed record DietEntry(MaterialId Material, float Digestibility);

    // One item kind and how much of it a carcass of this species holds - taken off in the fixed
    // order the species lists it (e.g. meat, hide, bone, sinew for a deer).
    public sealed record CarcassYield(ItemKindId Item, int Amount);

    // Group size and the shared HomeRange it spawns with - MinSize/MaxSize bound the herd's
    // actual size, HomeRadius is the wander radius around the shared anchor, and
    // DriftMetresPerSeason is the anchor's per-season move.
    public sealed record HerdDefinition(int MinSize, int MaxSize, float HomeRadius, float DriftMetresPerSeason);

    // The species' own mating rule: the climate she must be in to conceive (keyed on Climate,
    // never a Season), how long she then carries the pregnancy, the per-tick chance an eligible
    // female conceives, and how well fed (hunger below this threshold) she must be to count as
    // eligible at all.
    public sealed record BreedingDefinition(Climate Climate, long GestationTicks, float ConceptionChancePerTick, float SatietyHungerBelow);

    // A species' own flight rule: break off and move directly away from the nearest living
    // person once one is closer than FleeDistance, until the gap reaches SafeDistance, at
    // SpeedPerTick.
    public sealed record FleeDefinition(float FleeDistance, float SafeDistance, float SpeedPerTick);

    // Half-width of this species' footprint for collision resolution, in metres. Required, as is
    // the multiplier below, and required rather than defaulted: a species file that forgot either
    // would otherwise load as a creature with no footprint or no appetite rather than fail, and
    // System.Text.Json enforces `required` without any further annotation.
    public required float CollisionRadius { get; init; }

    // Multiplies the base hunger-per-tick rate for every creature of this species. 1 for a
    // human, so nothing about a person changes; below 1 for a species that needs to run leaner
    // through a lean season.
    public required float HungerPerTickMultiplier { get; init; }

    // C# does not allow a collection-expression default on the primary constructor parameters
    // above, so the empty-collection normalization happens here instead.
    public IReadOnlyList<DietEntry> Diet { get; } = Diet ?? [];

    public IReadOnlyList<TechniqueId> InnateTechniques { get; } = InnateTechniques ?? [];

    public IReadOnlyList<CarcassYield> Carcass { get; } = Carcass ?? [];

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
