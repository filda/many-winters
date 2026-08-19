using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;

namespace ManyWinters.Core.World;

public sealed record ResourceDefinition(
    ResourceKindId Id,
    string DisplayName,
    SkillTypeId Skill,
    ItemKindId? YieldsItem = null,
    IReadOnlyList<ClimateYield>? ClimateYields = null,
    float RegenPerTick = 0f)
{
    public float YieldMultiplierFor(Climate climate)
    {
        if (ClimateYields is null)
        {
            return 1f;
        }

        foreach (var entry in ClimateYields)
        {
            if (entry.Climate == climate)
            {
                return entry.Multiplier;
            }
        }

        return 1f;
    }
}
