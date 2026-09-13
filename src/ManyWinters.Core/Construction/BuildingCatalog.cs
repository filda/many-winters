using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.Construction;

public sealed class BuildingCatalog
{
    private readonly Dictionary<BuildingKindId, BuildingDefinition> _definitions;

    public BuildingCatalog(IEnumerable<BuildingDefinition> definitions)
    {
        _definitions = definitions.ToDictionary(d => d.Id);
    }

    public BuildingDefinition Get(BuildingKindId id) => _definitions[id];

    public static BuildingCatalog LoadFromDirectory(string rootPath)
        => LoadFromJson(JsonDefinitions.ReadDirectory(rootPath));

    // Takes documents, not a path: in an exported Godot build only Godot's file access reaches
    // the content inside the .pck.
    public static BuildingCatalog LoadFromJson(IEnumerable<(string Source, string Json)> documents)
        => new(JsonDefinitions.Parse<BuildingDefinition>(documents, "Building"));
}
