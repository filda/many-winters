using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.Items;

public sealed class ItemCatalog
{
    private readonly Dictionary<ItemKindId, ItemDefinition> _definitions;

    public ItemCatalog(IEnumerable<ItemDefinition> definitions)
    {
        _definitions = definitions.ToDictionary(d => d.Id);
    }

    public ItemDefinition Get(ItemKindId id) => _definitions[id];

    // Items without a definition (e.g. raw materials that carry no properties of their own)
    // simply provide no insulation, rather than every item needing a content entry.
    public float InsulationFor(ItemKindId id) => _definitions.TryGetValue(id, out var definition) ? definition.Insulation : 0f;

    // Same "missing definition = no effect" fallback as InsulationFor - an item with no
    // content entry is weightless (never gates carry capacity) and isn't food (can't be
    // eaten), rather than every item needing one just to be picked up at all.
    public float WeightFor(ItemKindId id) => _definitions.TryGetValue(id, out var definition) ? definition.Weight : 0f;

    public float HungerRestoredPerUnitFor(ItemKindId id) => _definitions.TryGetValue(id, out var definition) ? definition.HungerRestoredPerUnit : 0f;

    public float CarryCapacityBonusFor(ItemKindId id) => _definitions.TryGetValue(id, out var definition) ? definition.CarryCapacityBonus : 0f;

    public static ItemCatalog LoadFromDirectory(string rootPath)
        => LoadFromJson(JsonDefinitions.ReadDirectory(rootPath));

    // Takes documents rather than a path so an exported Godot build, where these live
    // inside the .pck and only Godot's file access can reach them, can load them too.
    public static ItemCatalog LoadFromJson(IEnumerable<(string Source, string Json)> documents)
        => new(JsonDefinitions.Parse<ItemDefinition>(documents, "Item"));
}
