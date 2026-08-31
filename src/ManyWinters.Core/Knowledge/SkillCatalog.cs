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

    // Unlike Get, doesn't assume the id is registered - WorldState's idle-AI checks for the
    // "eating"/"teaching" pseudo-skills unconditionally on every Advance, including for a
    // deliberately minimal or unrelated catalog (most unit tests) that never defined them.
    public SkillDefinition? Find(SkillTypeId id) => _definitions.GetValueOrDefault(id);

    // WorldState's own casual/ambient teaching (AutoTeachNearbyPeople) needs to tell every
    // registered skill's EfficientTechnique apart from its BaseTechnique - only the latter
    // ever spreads just from standing near someone.
    public IEnumerable<SkillDefinition> Definitions => _definitions.Values;

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
