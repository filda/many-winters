using ManyWinters.Core.Serialization;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Construction;

public sealed class BuildingCatalog
{
    private readonly Dictionary<EntityKindId, BuildingDefinition> _definitions;

    public BuildingCatalog(IEnumerable<BuildingDefinition> definitions)
    {
        _definitions = definitions.ToDictionary(d => d.Id);
    }

    public BuildingDefinition Get(EntityKindId id) => _definitions[id];

    // Every kind there is - for the menu of what a person could put up on a chosen spot, which
    // has to list the possibilities before it can offer one (see SkillCatalog.Definitions).
    public IEnumerable<BuildingDefinition> Definitions => _definitions.Values;

    public static BuildingCatalog LoadFromDirectory(string rootPath)
        => LoadFromJson(JsonDefinitions.ReadDirectory(rootPath));

    // Takes documents, not a path: in an exported Godot build only Godot's file access reaches
    // the content inside the .pck.
    public static BuildingCatalog LoadFromJson(IEnumerable<(string Source, string Json)> documents)
        => new(JsonDefinitions.Parse<BuildingDefinition>(documents, "Building"));
}
