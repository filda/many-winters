using ManyWinters.Core.Materials;

namespace ManyWinters.Core.Items;

// Two tiers, because they are two different kinds of thing (see
// docs/materials-and-crafting-architecture.md section 5). Raw materials stack - twelve grass is
// twelve of the same grass, and a count is the whole truth about them. A worked object does not:
// two cords twisted by different hands are not interchangeable, since each carries the quality
// its maker gave it, so each is held as itself.
public sealed class Inventory
{
    private readonly Dictionary<ItemKindId, int> _counts = new();
    private readonly List<Assembly> _assemblies = [];

    public IReadOnlyDictionary<ItemKindId, int> Counts => _counts;

    public IReadOnlyList<Assembly> Assemblies => _assemblies;

    public void AddAssembly(Assembly assembly) => _assemblies.Add(assembly);

    // Takes one thing equal to this one, not this exact object: an assembly is a value, so two
    // that match in every part and joint are the same thing to everyone who could tell them
    // apart. That stops holding true the day a worked thing carries a maker or its own wear, and
    // that is the day it needs an identity of its own (see
    // docs/materials-and-crafting-architecture.md section 6).
    public void RemoveAssembly(Assembly assembly) => _assemblies.Remove(assembly);

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

    // Both tiers weigh on the same scale, so a pack full of worked things is as heavy to carry
    // as the material that went into them.
    public float TotalWeight(ItemCatalog catalog) =>
        _counts.Sum(kv => catalog.WeightFor(kv.Key) * kv.Value) + _assemblies.Sum(catalog.WeightOf);

    // The best chopping-scored object carried, or 0 for empty-handed - what GatherCommand and
    // FellCommand ask instead of checking for one authored "tool" item kind.
    //
    // Both tiers answer, because a hafted axe is a worked object and a raw lump is a count, and
    // the question "what is the best thing in this pack to chop with" does not care which.
    public float BestChoppingScore(ItemCatalog catalog) =>
        _counts.Keys.Select(catalog.ChoppingScoreFor)
            .Concat(_assemblies.Select(catalog.ChoppingScoreOf))
            .DefaultIfEmpty(0f)
            .Max();

    // Takes the whole thing or none of it, and says which: a made object is one object, so
    // unlike a stack it cannot be taken as much as fits. A caller pulling from a corpse or a
    // store needs the answer to know whether to take it off the body.
    public bool AddAssemblyIfItFits(Assembly assembly, ItemCatalog catalog, float maxWeight)
    {
        var weight = catalog.WeightOf(assembly);

        // A weightless thing never limits, the same forgiveness UnitsThatFit gives a weightless
        // item kind.
        if (weight > 0f && TotalWeight(catalog) + weight > maxWeight)
        {
            return false;
        }

        AddAssembly(assembly);
        return true;
    }

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

    // Whether a single unit of `kind` would still fit - asked before walking to a source of it.
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
