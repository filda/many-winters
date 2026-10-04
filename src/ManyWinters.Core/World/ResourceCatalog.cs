using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.World;

public sealed class ResourceCatalog
{
    private readonly Dictionary<EntityKindId, ResourceDefinition> _definitions;

    public ResourceCatalog(IEnumerable<ResourceDefinition> definitions)
    {
        _definitions = definitions.ToDictionary(d => d.Id);
        MaxCollisionRadius = _definitions.Values.Select(d => d.CollisionRadius).DefaultIfEmpty(0f).Max();
    }

    // The widest any growing thing stands, so a collision search knows how far out an obstacle
    // can still reach a creature.
    public float MaxCollisionRadius { get; }

    public static ResourceCatalog LoadFromDirectory(string rootPath)
        => LoadFromJson(JsonDefinitions.ReadDirectory(rootPath));

    // Takes documents rather than a path so an exported Godot build, where these live
    // inside the .pck and only Godot's file access can reach them, can load them too.
    public static ResourceCatalog LoadFromJson(IEnumerable<(string Source, string Json)> documents)
        => new(JsonDefinitions.Parse<ResourceDefinition>(documents, "Resource"));

    public ResourceDefinition Get(EntityKindId id) => _definitions[id];
}
