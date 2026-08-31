using ManyWinters.Core.Serialization;

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
        => LoadFromJson(JsonDefinitions.ReadDirectory(rootPath));

    // Takes documents rather than a path so an exported Godot build, where these live
    // inside the .pck and only Godot's file access can reach them, can load them too.
    public static RecipeCatalog LoadFromJson(IEnumerable<(string Source, string Json)> documents)
        => new(JsonDefinitions.Parse<RecipeDefinition>(documents, "Recipe"));
}
