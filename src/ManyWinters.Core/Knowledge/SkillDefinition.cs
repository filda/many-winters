using ManyWinters.Core.Items;

namespace ManyWinters.Core.Knowledge;

public sealed record SkillDefinition(
    SkillTypeId Id,
    string DisplayName,
    TechniqueId EfficientTechnique,
    ItemKindId? Tool = null,
    float ToolHarvestBonus = 0f);
