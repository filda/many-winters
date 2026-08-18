using System.Text.Json;

namespace ManyWinters.Core.Knowledge;

public sealed class SkillCatalog
{
    private readonly Dictionary<SkillTypeId, SkillDefinition> _definitions;

    public SkillCatalog(IEnumerable<SkillDefinition> definitions)
    {
        _definitions = definitions.ToDictionary(d => d.Id);
    }

    public SkillDefinition Get(SkillTypeId id) => _definitions[id];

    public static SkillCatalog LoadFromDirectory(string rootPath)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var definitions = new List<SkillDefinition>();

        foreach (var directory in Directory.GetDirectories(rootPath))
        {
            foreach (var file in Directory.GetFiles(directory, "*.json"))
            {
                var json = File.ReadAllText(file);
                var definition = JsonSerializer.Deserialize<SkillDefinition>(json, options)
                    ?? throw new InvalidDataException($"Skill definition '{file}' could not be parsed.");
                definitions.Add(definition);
            }
        }

        return new SkillCatalog(definitions);
    }
}
