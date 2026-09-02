using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;

namespace ManyWinters.Core.World;

public sealed record ResourceDefinition(
    ResourceKindId Id,
    string DisplayName,
    SkillTypeId Skill,
    ItemKindId? YieldsItem = null,
    IReadOnlyList<ClimateYield>? ClimateYields = null,
    float RegenPerTick = 0f,
    bool CanFell = false,
    // Felling doesn't hand the yield straight to the person's inventory - it leaves behind an
    // ordinary resource node of this kind and amount (typically "wood") that still has to be
    // gathered like anything else, and stands in for the felled tree so the spot doesn't just
    // go empty.
    ResourceKindId? FellLeavesKind = null,
    float FellLeavesAmount = 0f,
    // How many ticks a node can sit in an IsInhospitable climate before it withers (see
    // WorldState.Advance). float.MaxValue - effectively never - unless a definition opts in
    // with a finite value; a stray 0-multiplier ClimateYield shouldn't kill something by
    // accident just because nobody set this.
    float TicksToWither = float.MaxValue,
    // How solid this resource's real-world footprint is, for WorldState.ResolveCollisions -
    // 0 (the default) means people can freely walk through it (grass, a mushroom, a tree
    // stump...). Deliberately independent of the billboard sprite's on-screen height
    // (ResourceVisualDefinition.WorldHeight, Godot-only): a tall sprite can still be a flat,
    // walk-through icon, and a short one (a rock pile) can still be genuinely solid.
    float CollisionRadius = 0f)
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

    // Conditions the plant doesn't thrive in at all, as opposed to just yielding less - the
    // set a definition describes positively via ClimateYields, read negatively.
    public bool IsInhospitable(Climate climate) => YieldMultiplierFor(climate) <= 0f;
}
