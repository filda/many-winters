using ManyWinters.Core.Population;

namespace ManyWinters.Tests.TestSupport;

// Person values a test has to supply but has no stake in, named so a reader can tell at a
// glance that the value carries no meaning rather than wondering what it is doing there.
public static class TestPeople
{
    // Sex is required of every Person (see Person.Sex), and most tests are about something
    // else entirely. A fixed value rather than one drawn from the id: a person whose sex is a
    // fresh coin flip per run is exactly what making it required was meant to end.
    public const Sex AnySex = Sex.Female;
}
