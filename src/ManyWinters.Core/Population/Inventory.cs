using ManyWinters.Core.Items;

namespace ManyWinters.Core.Population;

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
}
