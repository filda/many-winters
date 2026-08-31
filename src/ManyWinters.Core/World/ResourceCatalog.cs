using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.World;

public sealed class ResourceCatalog
{
    private readonly Dictionary<ResourceKindId, ResourceDefinition> _definitions;

    public ResourceCatalog(IEnumerable<ResourceDefinition> definitions)
    {
        _definitions = definitions.ToDictionary(d => d.Id);
    }

    public ResourceDefinition Get(ResourceKindId id) => _definitions[id];

    public static ResourceCatalog LoadFromDirectory(string rootPath)
        => LoadFromJson(JsonDefinitions.ReadDirectory(rootPath));

    // Takes documents rather than a path so an exported Godot build, where these live
    // inside the .pck and only Godot's file access can reach them, can load them too.
    public static ResourceCatalog LoadFromJson(IEnumerable<(string Source, string Json)> documents)
        => new(JsonDefinitions.Parse<ResourceDefinition>(documents, "Resource"));
}
