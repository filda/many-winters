using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

// Separation is the only thing allowed to move anyone here: every person is granted a long
// idle grace (see GrantIdleGraceCommand) so WorldState.Advance never drops them into an
// IdleTask and wanders them off, which is what makes exact positions assertable at all.
public class WorldStateCollisionTests
{
    private const float PersonRadius = 0.35f;
    private const double PersonMinDistance = PersonRadius * 2;

    [Fact]
    public void TwoPeopleStandingTooCloseAreEachPushedHalfTheOverlapApart()
    {
        // Half a metre apart with a 0.7m minimum leaves 0.2m of overlap, split evenly: each
        // moves 0.1m directly away from the other, along the line between them.
        var world = WorldWithPeople(new Position(0, 0), new Position(0.5, 0), out var a, out var b);

        world.Advance(1);

        AssertPosition(-0.1, 0, a.Position);
        AssertPosition(0.6, 0, b.Position);
    }

    [Fact]
    public void ThePushRunsAlongTheLineBetweenThemRatherThanOnOneAxis()
    {
        // A 3-4-5 overlap: the same 0.2m of overlap, but split between X and Y in proportion
        // to the offset, so nobody gets shoved sideways off the line they actually overlap on.
        var world = WorldWithPeople(new Position(0, 0), new Position(0.3, 0.4), out var a, out var b);

        world.Advance(1);

        AssertPosition(-0.06, -0.08, a.Position);
        AssertPosition(0.36, 0.48, b.Position);
    }

    [Fact]
    public void PeopleAlreadyFarEnoughApartAreNotMovedAtAll()
    {
        var world = WorldWithPeople(new Position(0, 0), new Position(5, 0), out var a, out var b);

        world.Advance(1);

        AssertPosition(0, 0, a.Position);
        AssertPosition(5, 0, b.Position);
    }

    [Fact]
    public void TwoPeopleStandingOnExactlyTheSameSpotStillComeApart()
    {
        // No direction to separate along, so rather than dividing by a zero distance (or
        // leaving them permanently fused) they take a fixed axis and get unstuck.
        var world = WorldWithPeople(new Position(3, 3), new Position(3, 3), out var a, out var b);

        world.Advance(1);

        Assert.True(WorldState.Distance(a.Position, b.Position) >= PersonMinDistance);
    }

    [Fact]
    public void SeparationEndsWithNobodyStillOverlapping()
    {
        var world = WorldWithPeople(new Position(0, 0), new Position(0.5, 0.1), out var a, out var b);

        world.Advance(1);

        Assert.True(WorldState.Distance(a.Position, b.Position) >= PersonMinDistance);
    }

    [Fact]
    public void APushBiggerThanOneTickAllowsIsClampedWithoutChangingItsDirection()
    {
        // Boxed in on one side by three boulders at once, the summed push runs well past what
        // a single tick may move someone. It gets scaled back to exactly the cap, and both
        // components have to be scaled by the same factor or the shove would come out crooked.
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), TestCatalogs.AdultAgeTicks);
        world.Execute(new GrantIdleGraceCommand(person, 1000));
        foreach (var offset in new[] { (0.10, 0.10), (0.15, 0.05), (0.05, 0.15) })
        {
            world.SpawnResourceNode(TestCatalogs.RockBoulder, new Position(offset.Item1, offset.Item2), amount: 10f);
        }

        world.Advance(1);

        var moved = WorldState.Distance(person.Position, new Position(0, 0));
        Assert.Equal(SimulationRules.Default.MaxCollisionPushPerTick, moved, precision: 6);

        // Pushed away from the cluster, which sits up and to the right of where they stood.
        Assert.True(person.Position.X < 0);
        Assert.True(person.Position.Y < 0);
    }

    [Fact]
    public void ADeadPersonIsNeitherPushedNorPushes()
    {
        var world = WorldWithPeople(new Position(0, 0), new Position(0.5, 0), out var a, out var b);
        b.IsAlive = false;

        world.Advance(1);

        AssertPosition(0, 0, a.Position);
        AssertPosition(0.5, 0, b.Position);
    }

    private static WorldState WorldWithPeople(Position first, Position second, out Person a, out Person b)
    {
        var world = TestCatalogs.CreateWorld();
        a = world.SpawnPerson("Ava", first, TestCatalogs.AdultAgeTicks);
        b = world.SpawnPerson("Bran", second, TestCatalogs.AdultAgeTicks);
        world.Execute(new GrantIdleGraceCommand(a, 1000));
        world.Execute(new GrantIdleGraceCommand(b, 1000));

        return world;
    }

    // Six places, not more: the minimum distance is computed in float (PersonCollisionRadius
    // is a float, so twice it is 0.69999998, not 0.7), which shows up in the seventh place of
    // every push derived from it. Still orders of magnitude tighter than any change to the
    // arithmetic itself would produce.
    private static void AssertPosition(double expectedX, double expectedY, Position actual)
    {
        Assert.Equal(expectedX, actual.X, precision: 6);
        Assert.Equal(expectedY, actual.Y, precision: 6);
    }
}
