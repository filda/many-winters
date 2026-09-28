using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

// Direct calls to Collisions.Resolve rather than through Advance - WorldStateCollisionTests
// already covers the same behaviour through a full tick, and stays as is.
public class CollisionsTests
{
    [Fact]
    public void TwoOverlappingCreaturesArePushedApartSymmetrically()
    {
        // Half a metre apart with a 0.7m minimum (two 0.35m person radii) leaves 0.2m of
        // overlap, split evenly: each moves 0.1m directly away from the other.
        var world = TestCatalogs.CreateWorld();
        var a = world.SpawnPerson("Ava", new Position(0, 0), TestCatalogs.AdultAgeTicks);
        var b = world.SpawnPerson("Bran", new Position(0.5, 0), TestCatalogs.AdultAgeTicks);

        Collisions.Resolve(world);

        // Six places: the minimum distance is computed in float (2 * 0.35f = 0.69999998),
        // which shows in the seventh place of the push derived from it.
        Assert.Equal(-0.1, a.Position.X, precision: 6);
        Assert.Equal(0.6, b.Position.X, precision: 6);
    }

    [Fact]
    public void APushBiggerThanTheCapIsClamped()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), TestCatalogs.AdultAgeTicks);
        foreach (var offset in new[] { (0.10, 0.10), (0.15, 0.05), (0.05, 0.15) })
        {
            world.SpawnResourceNode(TestCatalogs.RockBoulder, new Position(offset.Item1, offset.Item2), amount: 10f);
        }

        Collisions.Resolve(world);

        var moved = WorldState.Distance(person.Position, new Position(0, 0));
        Assert.Equal(SimulationRules.Default.MaxCollisionPushPerTick, moved, precision: 6);
    }
}
