using System.Text.Json;

namespace ManyWinters.Core.Items;

public sealed class RecipeCatalog
{
    private readonly Dictionary<ItemKindId, RecipeDefinition> _definitions;

    public RecipeCatalog(IEnumerable<RecipeDefinition> definitions)
    {
        _definitions = definitions.ToDictionary(d => d.Output);
    }

    public RecipeDefinition Get(ItemKindId output) => _definitions[output];

    public static RecipeCatalog LoadFromDirectory(string rootPath)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var definitions = new List<RecipeDefinition>();

        foreach (var directory in Directory.GetDirectories(rootPath))
        {
            foreach (var file in Directory.GetFiles(directory, "*.json"))
            {
                var json = File.ReadAllText(file);
                var definition = JsonSerializer.Deserialize<RecipeDefinition>(json, options)
                    ?? throw new InvalidDataException($"Recipe definition '{file}' could not be parsed.");
                definitions.Add(definition);
            }
        }

        return new RecipeCatalog(definitions);
    }
}
