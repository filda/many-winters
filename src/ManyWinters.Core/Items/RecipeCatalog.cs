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

    // Every recipe there is - for the menu of what a person could make, which has to list the
    // possibilities before it can offer one.
    public IEnumerable<RecipeDefinition> Definitions => _definitions.Values;

    public static RecipeCatalog LoadFromDirectory(string rootPath)
        => LoadFromJson(JsonDefinitions.ReadDirectory(rootPath));

    // Takes documents, not a path: in an exported Godot build only Godot's file access reaches
    // the content inside the .pck.
    public static RecipeCatalog LoadFromJson(IEnumerable<(string Source, string Json)> documents)
        => new(JsonDefinitions.Parse<RecipeDefinition>(documents, "Recipe"));
}
