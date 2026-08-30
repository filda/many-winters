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

    // Adds as much of `amount` as still fits under maxWeight (a zero-weight item is never
    // capacity-limited) and returns how many units actually got added, so a caller pulling
    // from a limited source (a resource node, a corpse, a building) only removes that many -
    // "take only what fits" rather than all-or-nothing.
    public int AddUpToCapacity(ItemKindId kind, int amount, ItemCatalog catalog, float maxWeight)
    {
        var unitWeight = catalog.WeightFor(kind);
        var toAdd = amount;
        if (unitWeight > 0f)
        {
            var remainingCapacity = maxWeight - TotalWeight(catalog);
            var fits = Math.Max(0, (int)(remainingCapacity / unitWeight));
            toAdd = Math.Min(amount, fits);
        }

        if (toAdd > 0)
        {
            Add(kind, toAdd);
        }

        return toAdd;
    }
}
