using ManyWinters.Core.Items;

namespace ManyWinters.Core.World;

// One kind of item dropped on the ground, at the spot it was dropped. Not an Inventory: a pile
// is always a single ItemKindId, dropped in one DropItemCommand call - dropping several kinds
// makes several piles, same as picking up a mixed handful was never a thing Inventory itself did.
public sealed class ItemPile
{
    public ItemPileId Id { get; init; } = ItemPileId.New();

    public required ItemKindId Kind { get; init; }

    public Position Position { get; init; }

    public int Amount { get; set; }
}
