using ManyWinters.Core.Population;

namespace ManyWinters.Tests.TestSupport;

// Person values a test has to supply but has no stake in, named so the reader can tell the
// value carries no meaning.
public static class TestPeople
{
    // Sex is required of every Person (see Person.Sex). A fixed value rather than one drawn from
    // the id, so a test's person is not a fresh coin flip per run.
    public const Sex AnySex = Sex.Female;
}
