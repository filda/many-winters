using ManyWinters.Core.World;

namespace ManyWinters.Core.Population;

// How much any two people have come to mean to each other - the first thing in this game that
// remembers something about a *pair* rather than about a person. Casual teaching already looks
// at pairs (WorldState.AutoTeachNearbyPeople) but deliberately keeps no state: its roll is a
// pure function of the two ids and the tick. This is the opposite, and has to be saved.
//
// Symmetric, and stored once per pair rather than once per direction. Real fondness is not
// mutual, but the thing being modelled here is time spent together, which is - and one number
// per pair instead of two is also half the state to grow, decay and write to disk.
public sealed class Affections
{
    private readonly Dictionary<(Guid Lower, Guid Higher), float> _byPair = new();

    // Every pair the world knows about, in no particular order - for saving, and for anything
    // that wants to look across all of them at once.
    public IEnumerable<(PersonId A, PersonId B, float Value)> All =>
        _byPair.Select(entry => (new PersonId(entry.Key.Lower), new PersonId(entry.Key.Higher), entry.Value));

    public float Between(PersonId a, PersonId b) => _byPair.GetValueOrDefault(PairOf(a, b));

    // Everyone this person has any bond with at all, strongest first. Pairs nobody has ever
    // been near are simply absent rather than present at zero, so this is the short list of
    // people who actually matter to them, not a row per inhabitant.
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

    // Moves a bond by `delta`, keeping it inside [0, max]. A bond that would fall to nothing is
    // dropped rather than kept at zero, so All and For stay the short list of pairs that ever
    // actually met (see For) instead of growing to every pair that once passed each other.
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

    // One key per unordered pair: the two ids sorted, so Between(a, b) and Between(b, a) reach
    // the same entry rather than two that can drift apart.
    private static (Guid Lower, Guid Higher) PairOf(PersonId a, PersonId b) =>
        a.Value.CompareTo(b.Value) <= 0 ? (a.Value, b.Value) : (b.Value, a.Value);
}
