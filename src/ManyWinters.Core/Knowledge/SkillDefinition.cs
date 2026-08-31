using ManyWinters.Core.Items;

namespace ManyWinters.Core.Knowledge;

// BaseTechnique is what makes a skill usable at all (see GatherCommand/FellCommand/EatCommand)
// - nobody is born knowing it, so the only way to ever get it is being taught: directly by the
// player (GrantTechniqueCommand, unconditional) or by another person who already knows it
// (TeachCommand, manually via right-click or autonomously between two people who happen to be
// near each other - see WorldState.Advance). EfficientTechnique still works the same way it
// always has on top of that - self-discoverable through repeated practice once the base
// technique lets that practice happen in the first place.
public sealed record SkillDefinition(
    SkillTypeId Id,
    string DisplayName,
    TechniqueId BaseTechnique,
    TechniqueId EfficientTechnique,
    ItemKindId? Tool = null,
    float ToolHarvestBonus = 0f);
