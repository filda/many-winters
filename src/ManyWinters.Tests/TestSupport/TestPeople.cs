using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.TestSupport;

// Person values a test has to supply but has no stake in, named so the reader can tell the
// value carries no meaning.
public static class TestPeople
{
    // Sex is required of every Person. A fixed value rather than one drawn from the id, so a
    // test's person is not a fresh coin flip per run.
    public const Sex AnySex = Sex.Female;

    // Home is required of every Person. A fresh camp each time, so no two tests share one, and
    // not in any world - a test that saves or ticks the world adds it there itself.
    public static HomeRange AnyHome => new(new Position(0, 0)) { Radius = 8f, DriftMetresPerSeason = 0f };
}
