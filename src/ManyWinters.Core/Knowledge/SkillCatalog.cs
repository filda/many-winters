using ManyWinters.Core.Serialization;

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
        => LoadFromJson(JsonDefinitions.ReadDirectory(rootPath));

    // Takes documents rather than a path so an exported Godot build, where these live
    // inside the .pck and only Godot's file access can reach them, can load them too.
    public static SkillCatalog LoadFromJson(IEnumerable<(string Source, string Json)> documents)
        => new(JsonDefinitions.Parse<SkillDefinition>(documents, "Skill"));
}
