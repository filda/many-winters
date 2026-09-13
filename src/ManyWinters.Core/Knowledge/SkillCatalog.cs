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

    // Unlike Get, tolerates an unregistered id: WorldState checks the "eating"/"teaching" skills
    // every Advance, also against minimal test catalogs that never defined them.
    public SkillDefinition? Find(SkillTypeId id) => _definitions.GetValueOrDefault(id);

    // WorldState.AutoTeachNearbyPeople tells every EfficientTechnique from a BaseTechnique -
    // only the latter spreads from standing near someone.
    public IEnumerable<SkillDefinition> Definitions => _definitions.Values;

    public static SkillCatalog LoadFromDirectory(string rootPath)
        => LoadFromJson(JsonDefinitions.ReadDirectory(rootPath));

    // Takes documents, not a path: in an exported Godot build only Godot's file access reaches
    // the content inside the .pck.
    public static SkillCatalog LoadFromJson(IEnumerable<(string Source, string Json)> documents)
        => new(JsonDefinitions.Parse<SkillDefinition>(documents, "Skill"));
}
