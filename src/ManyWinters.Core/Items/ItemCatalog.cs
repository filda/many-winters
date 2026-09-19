using ManyWinters.Core.Materials;
using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.Items;

public sealed class ItemCatalog
{
    private readonly Dictionary<ItemKindId, ItemDefinition> _definitions;
    private readonly MaterialCatalog _materials;

    public ItemCatalog(IEnumerable<ItemDefinition> definitions, MaterialCatalog materials)
    {
        _definitions = definitions.ToDictionary(d => d.Id);
        _materials = materials;
    }

    public ItemDefinition Get(ItemKindId id) => _definitions[id];

    // Insulation is the material's, whatever it was made into. An undescribed item or material
    // insulates nothing.
    public float InsulationFor(ItemKindId id) =>
        _definitions.TryGetValue(id, out var definition)
            ? _materials.Find(definition.Material)?.Insulation ?? 0f
            : 0f;

    // Density from the material, bulk from the item. Same missing-definition fallback as
    // InsulationFor: an undescribed item is weightless rather than unusable.
    public float WeightFor(ItemKindId id) =>
        _definitions.TryGetValue(id, out var definition) ? WeightOf(definition) : 0f;

    // Hardness times the square root of mass - a hard, heavy object chops well (see
    // docs/materials-and-crafting-architecture.md section 4). Placeholder until Form is read and
    // assemblies exist (step 4): the full formula there also wants an edge (from Form) and haft
    // leverage (from being bound to a shaft), neither of which exists yet, so today a raw lump of
    // the same hard material scores identically to a properly hafted axe. GatherCommand and
    // FellCommand read this instead of a per-skill authored "tool" item kind.
    public float ChoppingScoreFor(ItemKindId id) =>
        _definitions.TryGetValue(id, out var definition)
            ? (_materials.Find(definition.Material)?.Hardness ?? 0f) * MathF.Sqrt(WeightOf(definition))
            : 0f;

    private float WeightOf(ItemDefinition definition) => (_materials.Find(definition.Material)?.Density ?? 0f) * definition.Volume;

    public float HungerRestoredPerUnitFor(ItemKindId id) => _definitions.TryGetValue(id, out var definition) ? definition.HungerRestoredPerUnit : 0f;

    public float CarryCapacityBonusFor(ItemKindId id) => _definitions.TryGetValue(id, out var definition) ? definition.CarryCapacityBonus : 0f;

    public static ItemCatalog LoadFromDirectory(string rootPath, MaterialCatalog materials)
        => LoadFromJson(JsonDefinitions.ReadDirectory(rootPath), materials);

    // Takes documents, not a path: in an exported Godot build only Godot's file access reaches
    // the content inside the .pck.
    public static ItemCatalog LoadFromJson(IEnumerable<(string Source, string Json)> documents, MaterialCatalog materials)
        => new(JsonDefinitions.Parse<ItemDefinition>(documents, "Item"), materials);
}
