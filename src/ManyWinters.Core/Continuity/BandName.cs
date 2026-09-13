using ManyWinters.Core.Population;

namespace ManyWinters.Core.Continuity;

// A band is called after its eldest member - "Liska's people" - rather than numbered, so a
// later band finding the graves of an earlier one finds a name, not a round counter. Whoever
// was born first among everyone who ever belonged to the band, living or dead: nobody older
// can join later, so the name settles the moment the band exists and never moves.
public static class BandName
{
    public static string Of(IEnumerable<Person> members) => $"{EldestOf(members).Name}'s people";

    // Two people born the same tick are told apart by name, then by id, so the answer does not
    // depend on the order the world happens to list them in.
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
