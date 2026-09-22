using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;

namespace ManyWinters.Core.World;

public sealed record ResourceDefinition(
    EntityKindId Id,
    string DisplayName,
    SkillTypeId Skill,
    ItemKindId? YieldsItem = null,
    IReadOnlyList<ClimateYield>? ClimateYields = null,
    float RegenPerTick = 0f,
    bool CanFell = false,
    // Felling does not hand the yield to the inventory: it leaves one ordinary resource node per
    // entry (typically wood), gathered like anything else, standing in for the tree. The first
    // sits where the tree stood (a stump), further ones (a fallen log) land nearby. Empty for
    // anything that cannot be felled.
    IReadOnlyList<ResourceDefinition.FellLeaf>? FellLeaves = null,
    // Whether felling this needs the felling skill's Tool in hand - a trunk needs an axe, a bush
    // does not, even though both use woodcutting.
    bool RequiresToolToFell = false,
    // Ticks a node survives in an inhospitable climate. float.MaxValue means never, so a stray
    // 0-multiplier ClimateYield cannot kill something by accident.
    float TicksToWither = float.MaxValue,
    // Footprint radius used for collision resolution, in metres; 0 means walk-through (grass, a
    // stump). Independent of the sprite's on-screen height: a tall sprite can be a flat icon and
    // a short rock pile can be solid.
    float CollisionRadius = 0f)
{
    public sealed record FellLeaf(EntityKindId Kind, float Amount);

    // C# does not allow a collection-expression default on the primary constructor parameters
    // above, so the empty-collection normalization happens here instead.
    public IReadOnlyList<ClimateYield> ClimateYields { get; } = ClimateYields ?? [];

    public IReadOnlyList<FellLeaf> FellLeaves { get; } = FellLeaves ?? [];

    public float YieldMultiplierFor(Climate climate)
    {
        foreach (var entry in ClimateYields)
        {
            if (entry.Climate == climate)
            {
                return entry.Multiplier;
            }
        }

        return 1f;
    }

    // Conditions the plant does not grow in at all, read off ClimateYields.
    public bool IsInhospitable(Climate climate) => YieldMultiplierFor(climate) <= 0f;
}
