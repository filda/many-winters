using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.Tasks;

public class IdleTaskTests
{
    private static Person NewPerson(Position position) =>
        new() { Id = new PersonId(1), Name = "Ava", BirthTick = 0, Position = position };

    [Fact]
    public void IsNeverComplete()
    {
        var task = new IdleTask();

        Assert.False(task.IsComplete);
    }

    [Fact]
    public void AdvanceMovesThePersonInsteadOfLeavingThemFrozen()
    {
        var person = NewPerson(new Position(3, 4));
        var task = new IdleTask();

        task.Advance(person);

        Assert.NotEqual(new Position(3, 4), person.Position);
    }

    [Fact]
    public void WanderingNeverStraysFurtherThanTheMaxWanderRadiusFromWhereItStarted()
    {
        var start = new Position(3, 4);
        var person = NewPerson(start);
        var task = new IdleTask();

        for (var i = 0; i < 500; i++)
        {
            task.Advance(person);

            // Mirrors IdleTask's own private MaxWanderRadius - a generous epsilon covers
            // floating-point drift, not a looser radius. Each person draws their own radius
            // somewhere at or under this ceiling (see the "individual radius" test below).
            Assert.True(WorldState.Distance(start, person.Position) <= 4f + 0.01f);
        }
    }

    [Fact]
    public void TwoDifferentPeopleWanderIndependently()
    {
        var start = new Position(3, 4);
        var ava = new Person { Id = new PersonId(1), Name = "Ava", BirthTick = 0, Position = start };
        var bran = new Person { Id = new PersonId(2), Name = "Bran", BirthTick = 0, Position = start };
        var avaTask = new IdleTask();
        var branTask = new IdleTask();

        avaTask.Advance(ava);
        branTask.Advance(bran);

        Assert.NotEqual(ava.Position, bran.Position);
    }

    [Fact]
    public void ConsecutivePersonIdsDoNotWanderInLockstep()
    {
        // Regression guard for System.Random's legacy algorithm correlating badly on nearby
        // small integer seeds (exactly what sequential person ids are) - without the seed
        // avalanche in IdleTask.SeedFor, these two would land suspiciously close together on
        // every single tick, reading as synchronized rather than independent wandering.
        var start = new Position(0, 0);
        var ava = new Person { Id = new PersonId(1), Name = "Ava", BirthTick = 0, Position = start };
        var bran = new Person { Id = new PersonId(2), Name = "Bran", BirthTick = 0, Position = start };
        var avaTask = new IdleTask();
        var branTask = new IdleTask();

        var sawADivergentTick = false;
        for (var i = 0; i < 50; i++)
        {
            avaTask.Advance(ava);
            branTask.Advance(bran);

            if (WorldState.Distance(ava.Position, bran.Position) > 0.5f)
            {
                sawADivergentTick = true;
                break;
            }
        }

        Assert.True(sawADivergentTick);
    }

    [Fact]
    public void DifferentPeopleGetDifferentIndividualWanderRadiuses()
    {
        var start = new Position(0, 0);
        var people = Enumerable.Range(1, 5)
            .Select(id => new Person { Id = new PersonId(id), Name = $"Person {id}", BirthTick = 0, Position = start })
            .ToList();

        // Each person's own radius stays fixed leg after leg, but which value they each
        // landed on isn't the same for everyone - the farthest any single one of them ever
        // gets from their anchor across many legs approximates their individual radius, and
        // those maxima shouldn't all coincide.
        var farthestReached = people.Select(person =>
        {
            var task = new IdleTask();
            var farthest = 0.0;
            for (var i = 0; i < 500; i++)
            {
                task.Advance(person);
                farthest = Math.Max(farthest, WorldState.Distance(start, person.Position));
            }

            return farthest;
        });

        Assert.True(farthestReached.Distinct().Count() > 1);
    }

    [Fact]
    public void TheSamePersonWandersTheSameWayFromAFreshTask()
    {
        var start = new Position(3, 4);
        var first = NewPerson(start);
        var second = NewPerson(start);

        new IdleTask().Advance(first);
        new IdleTask().Advance(second);

        Assert.Equal(first.Position, second.Position);
    }
}
