namespace ManyWinters.Core.Population.Naming;

// A frequency table sampled by weight rather than picked uniformly - what `CultureProfile` is a
// collection of, one per naming feature (onsets, nuclei, codas, syllable counts). Insertion
// order backs sampling instead of a Dictionary's enumeration order, which .NET does not promise
// to hold still, and this has to replay identically every time (see WorldState.NameForNewborn).
public sealed class WeightedSet<T>
    where T : notnull
{
    private readonly List<T> _items = new();
    private readonly List<float> _weights = new();
    private readonly Dictionary<T, int> _indexOf = new();

    public float TotalWeight { get; private set; }

    public bool IsEmpty => _items.Count == 0 || TotalWeight <= 0f;

    public void Add(T item, float weight)
    {
        if (weight <= 0f)
        {
            return;
        }

        if (_indexOf.TryGetValue(item, out var index))
        {
            _weights[index] += weight;
        }
        else
        {
            _indexOf[item] = _items.Count;
            _items.Add(item);
            _weights.Add(weight);
        }

        TotalWeight += weight;
    }

    // Fades every weight toward zero without discarding it outright - CultureProfile's slow
    // decay, applied once per historical name it replays (CultureProfile.Build).
    public void DecayAll(float factor)
    {
        for (var i = 0; i < _weights.Count; i++)
        {
            _weights[i] *= factor;
        }

        TotalWeight *= factor;
    }

    public T Sample(Random rng)
    {
        if (IsEmpty)
        {
            throw new InvalidOperationException("Cannot sample an empty weighted set.");
        }

        var roll = rng.NextDouble() * TotalWeight;
        var cumulative = 0.0;
        for (var i = 0; i < _items.Count; i++)
        {
            cumulative += _weights[i];
            if (roll < cumulative)
            {
                return _items[i];
            }
        }

        // Floating-point rounding can leave `roll` a hair past the last cumulative sum; the last
        // item is the only sound fallback, not a sign anything above is wrong.
        return _items[^1];
    }

    public IEnumerable<(T Item, float Weight)> Enumerate()
    {
        for (var i = 0; i < _items.Count; i++)
        {
            yield return (_items[i], _weights[i]);
        }
    }
}
