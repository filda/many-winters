using System.Text.Json;

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
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var definitions = new List<BuildingDefinition>();

        foreach (var directory in Directory.GetDirectories(rootPath))
        {
            foreach (var file in Directory.GetFiles(directory, "*.json"))
            {
                var json = File.ReadAllText(file);
                var definition = JsonSerializer.Deserialize<BuildingDefinition>(json, options)
                    ?? throw new InvalidDataException($"Building definition '{file}' could not be parsed.");
                definitions.Add(definition);
            }
        }

        return new BuildingCatalog(definitions);
    }
}
