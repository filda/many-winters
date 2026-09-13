using ManyWinters.Core.Population;

namespace ManyWinters.Core.Continuity;

// A band is named after its eldest member ever, living or dead - "Liska's people" - so a later
// band finding its graves finds a name, not a counter. Nobody older can join later, so the
// name never moves.
public static class BandName
{
    public static string Of(IEnumerable<Person> members) => $"{EldestOf(members).Name}'s people";

    // Same-tick births are ordered by name, then id, so the answer does not depend on list order.
    // Stryker disable Linq: which of two same-named ids wins is arbitrary (only that the same one
    // wins from either list order matters), and a band with nobody in it fails either way - First
    // throws, FirstOrDefault hands back a null the caller dereferences
    public static Person EldestOf(IEnumerable<Person> members) =>
        members
            .OrderBy(person => person.BirthTick)
            .ThenBy(person => person.Name, StringComparer.Ordinal)
            .ThenBy(person => person.Id.Value)
            .First();

    // Stryker restore Linq
}
