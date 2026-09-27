using ManyWinters.Core.Items;
using ManyWinters.Core.Materials;

namespace ManyWinters.Core.World;

// What a kind of Entity does, so presentation code can pick a view without guessing from which
// optional components happen to be populated. Extend this, not the component set, when a
// genuinely new kind of thing (a chest, a machine) arrives.
public enum EntityCategory
{
    Growable,
    Pile,
    Building,
}

// One thing that exists on the map at a position, identified by a kind - a tree, a dropped pile
// of apples, a storage hut. Replaces the formerly separate ResourceNode/ItemPile/Building
// classes: they differed only in which of these optional components they carried, not in what
// they fundamentally were.
public sealed class Entity
{
    public EntityId Id { get; init; } = EntityId.New();

    public required EntityKindId Kind { get; init; }

    public required EntityCategory Category { get; init; }

    public Position Position { get; init; }

    // Grows, regenerates, dies - a tree, a bush, a patch of ground cover. Null for anything that
    // doesn't (a pile, a building).
    public GrowthState? Growth { get; init; }

    // A static, non-regenerating stock - a pile someone drops. Null for anything else.
    // Floor-gated like GrowthState.RemainingAmount, but never regenerates and deletes the
    // entity once it reaches zero.
    public int? StaticAmount { get; set; }

    // When a StaticAmount pile came to be, for the spoilage pass - one tick for the whole pile
    // rather than a ledger like Inventory's, since a pile is one drop, not a pack built up over
    // time. Set when the pile is dropped; null for anything that isn't a stock pile (a Made thing
    // ages by its own Assembly.MadeTick instead, and a Growable resource never spoils - it grows).
    public long? DroppedTick { get; init; }

    // One worked object lying where somebody put it down - a cord, an axe. Null for a pile of
    // raw stock, which is a count and not a thing: the inventory's two tiers show up on the
    // ground as they do in a pack, and a pile holds exactly one of them.
    public Assembly? Made { get; init; }

    // Durability - a building's Condition. Ceiling-gated (repair caps at max) rather than
    // floor-gated: unlike RemainingAmount/StaticAmount, nothing today treats zero as "gone".
    public float? Condition { get; set; }

    public Inventory? Storage { get; init; }
}
