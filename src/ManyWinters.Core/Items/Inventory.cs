namespace ManyWinters.Core.Items;

public sealed class Inventory
{
    private readonly Dictionary<ItemKindId, int> _counts = new();

    public IReadOnlyDictionary<ItemKindId, int> Counts => _counts;

    public int Get(ItemKindId kind) => _counts.GetValueOrDefault(kind);

    public void Add(ItemKindId kind, int amount) => _counts[kind] = Get(kind) + amount;

    public bool Remove(ItemKindId kind, int amount)
    {
        var current = Get(kind);
        if (current < amount)
        {
            return false;
        }

        var remaining = current - amount;
        if (remaining == 0)
        {
            _counts.Remove(kind);
        }
        else
        {
            _counts[kind] = remaining;
        }

        return true;
    }

    public float TotalWeight(ItemCatalog catalog) => _counts.Sum(kv => catalog.WeightFor(kv.Key) * kv.Value);

    // The best chopping-scored object carried, or 0 for empty-handed - what GatherCommand and
    // FellCommand ask instead of checking for one authored "tool" item kind (see
    // ItemCatalog.ChoppingScoreFor).
    public float BestChoppingScore(ItemCatalog catalog) => _counts.Keys.Select(catalog.ChoppingScoreFor).DefaultIfEmpty(0f).Max();

    // Adds as much of `amount` as fits under maxWeight (a zero-weight item never limits) and
    // returns how many, so a caller pulling from a node, corpse or building removes only that many.
    public int AddUpToCapacity(ItemKindId kind, int amount, ItemCatalog catalog, float maxWeight)
    {
        var toAdd = Math.Min(amount, UnitsThatFit(kind, catalog, maxWeight));
        if (toAdd > 0)
        {
            Add(kind, toAdd);
        }

        return toAdd;
    }

    // Whether a single unit of `kind` would still fit - asked before walking to a source of it
    // (see GatherCommand.CanTakeAnythingFrom).
    public bool HasRoomFor(ItemKindId kind, ItemCatalog catalog, float maxWeight) => UnitsThatFit(kind, catalog, maxWeight) > 0;

    private int UnitsThatFit(ItemKindId kind, ItemCatalog catalog, float maxWeight)
    {
        var unitWeight = catalog.WeightFor(kind);
        if (unitWeight <= 0f)
        {
            return int.MaxValue;
        }

        var remainingCapacity = maxWeight - TotalWeight(catalog);
        return Math.Max(0, (int)(remainingCapacity / unitWeight));
    }
}
