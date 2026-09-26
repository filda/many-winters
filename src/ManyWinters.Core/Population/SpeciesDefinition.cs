using ManyWinters.Core.Materials;

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
    IReadOnlyList<SpeciesDefinition.DietEntry>? Diet = null)
{
    public sealed record DietEntry(MaterialId Material, float Digestibility);

    // C# does not allow a collection-expression default on the primary constructor parameters
    // above, so the empty-collection normalization happens here instead (see
    // ResourceDefinition.ClimateYields).
    public IReadOnlyList<DietEntry> Diet { get; } = Diet ?? [];

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
