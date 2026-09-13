using ManyWinters.Core.Items;

namespace ManyWinters.Core.Knowledge;

// BaseTechnique makes a skill usable at all (see GatherCommand/FellCommand/EatCommand). Nobody
// is born knowing it; it comes only from the player (GrantTechniqueCommand) or from another
// person (TeachCommand, directed or autonomous - see WorldState.Advance). EfficientTechnique is
// self-discovered through practice once the base technique lets practice happen.
public sealed record SkillDefinition(
    SkillTypeId Id,
    string DisplayName,
    TechniqueId BaseTechnique,
    TechniqueId EfficientTechnique,
    ItemKindId? Tool = null,
    float ToolHarvestBonus = 0f);
