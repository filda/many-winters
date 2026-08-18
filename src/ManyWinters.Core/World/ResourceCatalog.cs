using System.Text.Json;

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
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var definitions = new List<ResourceDefinition>();

        foreach (var directory in Directory.GetDirectories(rootPath))
        {
            foreach (var file in Directory.GetFiles(directory, "*.json"))
            {
                var json = File.ReadAllText(file);
                var definition = JsonSerializer.Deserialize<ResourceDefinition>(json, options)
                    ?? throw new InvalidDataException($"Resource definition '{file}' could not be parsed.");
                definitions.Add(definition);
            }
        }

        return new ResourceCatalog(definitions);
    }
}
