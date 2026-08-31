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

    // Takes documents rather than a path so an exported Godot build, where these live
    // inside the .pck and only Godot's file access can reach them, can load them too.
    public static BuildingCatalog LoadFromJson(IEnumerable<(string Source, string Json)> documents)
        => new(JsonDefinitions.Parse<BuildingDefinition>(documents, "Building"));
}
