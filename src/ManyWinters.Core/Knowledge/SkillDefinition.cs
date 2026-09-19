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
    // Whether gathering with this skill takes a bonus from the best chopping-scored object the
    // person carries (see ItemCatalog.ChoppingScoreFor) - no longer naming one specific item as
    // "the tool" (docs/materials-and-crafting-architecture.md section 4): any hard enough object
    // scores, not just the one authored as this skill's tool.
    bool UsesChoppingScore = false);
