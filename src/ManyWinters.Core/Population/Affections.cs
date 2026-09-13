using ManyWinters.Core.World;

namespace ManyWinters.Core.Population;

// How much two people have come to mean to each other - the only pair-level state in the game
// (casual teaching also looks at pairs but keeps no state). Symmetric and stored once per pair:
// what is modelled is time spent together, which is mutual, and it halves the state to grow,
// decay and save.
public sealed class Affections
{
    private readonly Dictionary<(Guid Lower, Guid Higher), float> _byPair = new();

    // Every pair the world knows about, in no particular order - for saving.
    public IEnumerable<(PersonId A, PersonId B, float Value)> All =>
        _byPair.Select(entry => (new PersonId(entry.Key.Lower), new PersonId(entry.Key.Higher), entry.Value));

    public float Between(PersonId a, PersonId b) => _byPair.GetValueOrDefault(PairOf(a, b));

    // Everyone this person has any bond with, strongest first. Pairs that never met are absent
    // rather than zero, so this is the short list of people who matter to them.
    public IEnumerable<(PersonId Other, float Value)> For(PersonId person) =>
        _byPair
            .Where(entry => entry.Key.Lower == person.Value || entry.Key.Higher == person.Value)
            .Select(entry => (Other: new PersonId(entry.Key.Lower == person.Value ? entry.Key.Higher : entry.Key.Lower), entry.Value))
            .OrderByDescending(bond => bond.Value);

    public void Set(PersonId a, PersonId b, float value)
    {
        if (a == b)
        {
            throw new ArgumentException("A person has no bond with themselves.", nameof(b));
        }

        _byPair[PairOf(a, b)] = value;
    }

    // Moves a bond by `delta` within [0, max]. A bond that falls to nothing is dropped, not kept
    // at zero, so All and For stay the short list of pairs that ever met.
    public void Change(PersonId a, PersonId b, float delta, float max)
    {
        var value = Math.Clamp(Between(a, b) + delta, 0f, max);
        if (value <= 0f)
        {
            _byPair.Remove(PairOf(a, b));
            return;
        }

        Set(a, b, value);
    }

    // One key per unordered pair, so Between(a, b) and Between(b, a) reach the same entry.
    private static (Guid Lower, Guid Higher) PairOf(PersonId a, PersonId b) =>
        a.Value.CompareTo(b.Value) <= 0 ? (a.Value, b.Value) : (b.Value, a.Value);
}
