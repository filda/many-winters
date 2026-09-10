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
    // Felling doesn't hand the yield straight to the person's inventory - it leaves behind one
    // ordinary resource node per entry (typically "wood") that still has to be gathered like
    // anything else, and stands in for the felled tree so the spot doesn't just go empty. A
    // tree yields more wood than fits in one inventory at once, hence more than one entry: the
    // first sits exactly where the tree stood (a stump), any further ones (a fallen log) land
    // nearby instead of stacking invisibly on top of it - see FellCommand.
    IReadOnlyList<ResourceDefinition.FellLeaf>? FellLeaves = null,
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
    public sealed record FellLeaf(ResourceKindId Kind, float Amount);

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
