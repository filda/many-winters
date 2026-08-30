using System.Text.Json;

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

    public static ItemCatalog LoadFromDirectory(string rootPath)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var definitions = new List<ItemDefinition>();

        foreach (var directory in Directory.GetDirectories(rootPath))
        {
            foreach (var file in Directory.GetFiles(directory, "*.json"))
            {
                var json = File.ReadAllText(file);
                var definition = JsonSerializer.Deserialize<ItemDefinition>(json, options)
                    ?? throw new InvalidDataException($"Item definition '{file}' could not be parsed.");
                definitions.Add(definition);
            }
        }

        return new ItemCatalog(definitions);
    }
}
