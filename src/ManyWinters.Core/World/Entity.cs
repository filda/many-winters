using ManyWinters.Core.Items;

namespace ManyWinters.Core.World;

// What a kind of Entity does, so presentation code can pick a view without guessing from which
// optional components happen to be populated (see WorldPresenter). Extend this, not the
// component set, when a genuinely new kind of thing (a chest, a machine) arrives.
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

    // A static, non-regenerating stock - a pile dropped by DropItemCommand. Null for anything
    // else. Floor-gated like GrowthState.RemainingAmount, but never regenerates and deletes the
    // entity once it reaches zero (see PickUpItemCommand).
    public int? StaticAmount { get; set; }

    // Durability - a building's Condition. Ceiling-gated (repair caps at max) rather than
    // floor-gated: unlike RemainingAmount/StaticAmount, nothing today treats zero as "gone".
    public float? Condition { get; set; }

    // Storage - a building's inventory. Null for anything that doesn't store items.
    public Inventory? Storage { get; init; }
}
