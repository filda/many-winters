using ManyWinters.Core.World;

namespace ManyWinters.Tests.TestSupport;

// An id whose seed (EntityId.SeedOf) is exactly the given number - for tests that pin a
// seed-driven outcome (a wander path, say) to a small, readable seed rather than a random Guid.
public static class TestIds
{
    public static PersonId Person(int seed) => new(new Guid(seed, 0, 0, new byte[8]));
}
