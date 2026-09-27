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

    // FIFO age ledger, kept only for a kind whose material has a shelf life
    // (ItemCatalog.ShelfLifeFor) - docs/todo/fauna-plan.md phase 4c, "one rule, three places".
    // A kind with no entries here is either non-perishable or was only ever added through the
    // plain, untimed Add/Remove below (test setup that does not care when something came to be);
    // either way it never expires. The sum of a kind's entries' counts never exceeds
    // _counts[kind] - it can fall short, for stock added without a tick - which is the invariant
    // Expire and the transfer methods below all preserve.
    private readonly Dictionary<ItemKindId, List<(long Tick, int Count)>> _ages = new();

    public IReadOnlyDictionary<ItemKindId, int> Counts => _counts;

    public IReadOnlyList<Assembly> Assemblies => _assemblies;

    // For SaveGameService: the ledger entries kept for each perishable kind actually carried,
    // oldest first isn't guaranteed here (Expire/consumption sort on demand) but every entry
    // belongs to a kind that Counts also lists.
    public IReadOnlyDictionary<ItemKindId, IReadOnlyList<(long Tick, int Count)>> Ages =>
        _ages.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<(long, int)>)kv.Value);

    public void AddAssembly(Assembly assembly) => _assemblies.Add(assembly);

    // Takes one thing equal to this one, not this exact object: an assembly is a value, so two
    // that match in every part and joint are the same thing to everyone who could tell them
    // apart. That stops holding true the day a worked thing carries a maker or its own wear, and
    // that is the day it needs an identity of its own (see
    // docs/materials-and-crafting-architecture.md section 6).
    public void RemoveAssembly(Assembly assembly) => _assemblies.Remove(assembly);

    public int Get(ItemKindId kind) => _counts.GetValueOrDefault(kind);

    // Untimed: for test setup, and for anything (a raw material) whose age nobody has ever asked
    // about. Never enrolls the units added in the age ledger, so they are exempt from Expire
    // rather than instantly the oldest thing in the pack - see the age-aware overload below for
    // stuff coming into being for real.
    public void Add(ItemKindId kind, int amount) => _counts[kind] = Get(kind) + amount;

    // A thing coming into existence right now - picked, butchered, made (docs/todo/fauna-plan.md
    // phase 4c). `tick` is when it came to be, not necessarily "this instant": FillCarcass backdates
    // to the creature's own DeathTick. Enrolls in the age ledger only if `kind`'s material has a
    // shelf life; a non-perishable kind added this way behaves exactly like the untimed overload.
    public void Add(ItemKindId kind, int amount, long tick, ItemCatalog catalog)
    {
        _counts[kind] = Get(kind) + amount;
        if (catalog.ShelfLifeFor(kind) is not null)
        {
            AgedEntriesFor(kind).Add((tick, amount));
        }
    }

    // For SaveGameService only: restores one saved ledger entry verbatim, trusting the save
    // rather than re-deriving perishability from a catalog (a save holds only what was actually
    // tracked, so if this is called at all the kind is meant to be tracked).
    public void RestoreAgedEntry(ItemKindId kind, long tick, int count) => AgedEntriesFor(kind).Add((tick, count));

    private List<(long Tick, int Count)> AgedEntriesFor(ItemKindId kind)
    {
        if (!_ages.TryGetValue(kind, out var entries))
        {
            entries = [];
            _ages[kind] = entries;
        }

        return entries;
    }

    public bool Remove(ItemKindId kind, int amount) => RemoveDated(kind, amount) is not null;

    // The same removal, but reporting which ledger entries (oldest tick first) it actually took -
    // what DropCommand needs to give a ground pile the oldest of the ages it holds, rather than
    // stamping the pile "now" and quietly refreshing every unit's age (docs/todo/fauna-plan.md
    // phase 4c: moving something does not change its age, dropping it included). Null means
    // there was not enough to remove, exactly like Remove's false; an empty (non-null) list means
    // the removal succeeded but none of it was tracked (non-perishable, or added untimed).
    public IReadOnlyList<(long Tick, int Count)>? RemoveDated(ItemKindId kind, int amount)
    {
        var current = Get(kind);
        if (current < amount)
        {
            return null;
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

        return ConsumeOldest(kind, amount);
    }

    // Removes up to `amount` from the age ledger, oldest tick first, and reports exactly what was
    // taken - the shape a transfer needs to hand the very same entries on to another Inventory
    // rather than restamping them "now" (docs/todo/fauna-plan.md phase 4c: moving something
    // between containers does not change its age). Whatever of `amount` the ledger cannot cover
    // (untimed stock, or a shortfall) is simply not reported - it was never aged to begin with.
    private List<(long Tick, int Count)> ConsumeOldest(ItemKindId kind, int amount)
    {
        var taken = new List<(long, int)>();
        if (!_ages.TryGetValue(kind, out var entries))
        {
            return taken;
        }

        entries.Sort((a, b) => a.Tick.CompareTo(b.Tick));

        var remaining = amount;
        var consumedWhole = 0;
        while (consumedWhole < entries.Count && remaining > 0)
        {
            var (tick, count) = entries[consumedWhole];
            if (count <= remaining)
            {
                taken.Add((tick, count));
                remaining -= count;
                consumedWhole++;
            }
            else
            {
                taken.Add((tick, remaining));
                entries[consumedWhole] = (tick, count - remaining);
                remaining = 0;
            }
        }

        if (consumedWhole > 0)
        {
            entries.RemoveRange(0, consumedWhole);
        }

        if (entries.Count == 0)
        {
            _ages.Remove(kind);
        }

        return taken;
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
    // `tick` is when these units came into being, exactly as the age-aware Add overload reads it -
    // 0 by default, which only matters for a kind with a shelf life, so every existing caller
    // (all of them non-perishable stock in practice) is unaffected.
    public int AddUpToCapacity(ItemKindId kind, int amount, ItemCatalog catalog, float maxWeight, long tick = 0)
    {
        var toAdd = Math.Min(amount, UnitsThatFit(kind, catalog, maxWeight));
        if (toAdd > 0)
        {
            Add(kind, toAdd, tick, catalog);
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

    // Moving something that already exists into another Inventory's uncapped room (a store's own
    // shelf - DepositCommand) - the age-preserving half of the "one rule" (docs/todo/fauna-plan.md
    // phase 4c): unlike Add, this is never a thing coming into being, so whatever ticks its units
    // already carried travel with them rather than being restamped "now".
    public int Transfer(ItemKindId kind, int amount, Inventory destination)
    {
        var toMove = Math.Min(amount, Get(kind));
        if (toMove <= 0)
        {
            return 0;
        }

        MoveUnits(kind, toMove, destination);
        return toMove;
    }

    // The capped counterpart (WithdrawCommand, PickUpItemCommand's stock case, ButcherCommand,
    // LootCommand): only what fits in `destination` under `maxWeight` changes hands, the rest
    // stays where it was.
    public int TransferUpToCapacity(ItemKindId kind, int amount, Inventory destination, ItemCatalog catalog, float maxWeight)
    {
        var toMove = Math.Min(amount, destination.UnitsThatFit(kind, catalog, maxWeight));
        toMove = Math.Min(toMove, Get(kind));
        if (toMove <= 0)
        {
            return 0;
        }

        MoveUnits(kind, toMove, destination);
        return toMove;
    }

    private void MoveUnits(ItemKindId kind, int amount, Inventory destination)
    {
        var carriedAges = ConsumeOldest(kind, amount);

        var remaining = Get(kind) - amount;
        if (remaining <= 0)
        {
            _counts.Remove(kind);
        }
        else
        {
            _counts[kind] = remaining;
        }

        destination._counts[kind] = destination.Get(kind) + amount;
        if (carriedAges.Count > 0)
        {
            destination.AgedEntriesFor(kind).AddRange(carriedAges);
        }
    }

    // The once-per-tick spoilage pass (WorldState.Advance, docs/todo/fauna-plan.md phase 4c):
    // drops every stacked unit and every worked object whose time is up, wherever this Inventory
    // sits (a pack, a carcass, a store's shelves). Returns what stock was lost, for tests/logging;
    // a lost assembly is simply gone from Assemblies, the same as RemoveAssembly leaves no trace.
    public IReadOnlyList<(ItemKindId Kind, int Count)> Expire(long currentTick, ItemCatalog catalog)
    {
        var lost = new List<(ItemKindId, int)>();

        foreach (var kind in _ages.Keys.ToList())
        {
            var shelfLife = catalog.ShelfLifeFor(kind);
            if (shelfLife is not { } shelf)
            {
                continue;
            }

            var entries = _ages[kind];
            entries.Sort((a, b) => a.Tick.CompareTo(b.Tick));

            var expiredCount = 0;
            var i = 0;
            while (i < entries.Count && currentTick - entries[i].Tick >= shelf)
            {
                expiredCount += entries[i].Count;
                i++;
            }

            if (i > 0)
            {
                entries.RemoveRange(0, i);
                if (entries.Count == 0)
                {
                    _ages.Remove(kind);
                }
            }

            if (expiredCount > 0)
            {
                var remaining = Get(kind) - expiredCount;
                if (remaining <= 0)
                {
                    _counts.Remove(kind);
                }
                else
                {
                    _counts[kind] = remaining;
                }

                lost.Add((kind, expiredCount));
            }
        }

        _assemblies.RemoveAll(assembly =>
            catalog.ShelfLifeTicksOf(assembly) is { } shelf && currentTick - assembly.MadeTick >= shelf);

        return lost;
    }
}
