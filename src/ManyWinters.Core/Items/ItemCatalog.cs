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

    // Insulation is the material's, not the item's - hide keeps the cold out whatever it has
    // been made into. An item with no definition, or one made of a material nobody described,
    // simply insulates nothing, rather than every item needing a content entry.
    public float InsulationFor(ItemKindId id) =>
        _definitions.TryGetValue(id, out var definition)
            ? _materials.Find(definition.Material)?.Insulation ?? 0f
            : 0f;

    // Density from the material, bulk from the item: the same stone is heavier as a boulder
    // than as a flake, and the same shape is heavier in stone than in wood. Same "missing
    // definition = no effect" fallback as InsulationFor, so an undescribed item is weightless
    // (never gates carry capacity) rather than unusable.
    public float WeightFor(ItemKindId id) =>
        _definitions.TryGetValue(id, out var definition)
            ? (_materials.Find(definition.Material)?.Density ?? 0f) * definition.Volume
            : 0f;

    public float HungerRestoredPerUnitFor(ItemKindId id) => _definitions.TryGetValue(id, out var definition) ? definition.HungerRestoredPerUnit : 0f;

    public float CarryCapacityBonusFor(ItemKindId id) => _definitions.TryGetValue(id, out var definition) ? definition.CarryCapacityBonus : 0f;

    public static ItemCatalog LoadFromDirectory(string rootPath, MaterialCatalog materials)
        => LoadFromJson(JsonDefinitions.ReadDirectory(rootPath), materials);

    // Takes documents rather than a path so an exported Godot build, where these live
    // inside the .pck and only Godot's file access can reach them, can load them too.
    public static ItemCatalog LoadFromJson(IEnumerable<(string Source, string Json)> documents, MaterialCatalog materials)
        => new(JsonDefinitions.Parse<ItemDefinition>(documents, "Item"), materials);
}
