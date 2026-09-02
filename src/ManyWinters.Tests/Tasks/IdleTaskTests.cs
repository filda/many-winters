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

        // A person pauses briefly before setting off (see IdleTask's own MaxPauseTicks) -
        // enough ticks to clear even the longest possible pause before a leg starts.
        for (var i = 0; i < 20; i++)
        {
            task.Advance(person);
        }

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
            Assert.True(WorldState.Distance(start, person.Position) <= 8f + 0.01f);
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

        // Clears even the longest possible pre-leg pause (see IdleTask's own MaxPauseTicks)
        // for both of them before comparing positions.
        for (var i = 0; i < 20; i++)
        {
            avaTask.Advance(ava);
            branTask.Advance(bran);
        }

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
    public void EachPersonGetsTheirOwnWanderRadiusSpreadAcrossTheWholeBand()
    {
        var start = new Position(0, 0);

        // Each person's own radius stays fixed leg after leg, but which value they each
        // landed on isn't the same for everyone - the farthest any single one of them ever
        // gets from their anchor across many legs approximates their individual radius. Those
        // maxima must not only differ, they have to spread across IdleTask's own 3..8 band:
        // a homebody who barely leaves the anchor and a roamer out near the ceiling, rather
        // than everyone clustered at one end of it.
        var farthestReached = Enumerable.Range(1, 30).Select(id =>
        {
            var person = new Person { Id = new PersonId(id), Name = $"Person {id}", BirthTick = 0, Position = start };
            var task = new IdleTask();
            var farthest = 0.0;
            for (var i = 0; i < 800; i++)
            {
                task.Advance(person);
                farthest = Math.Max(farthest, WorldState.Distance(start, person.Position));
            }

            return farthest;
        }).ToList();

        Assert.All(farthestReached, f => Assert.True(f <= 8f + 0.01f, $"Someone roamed {f} from their anchor."));
        Assert.True(farthestReached.Max() > 7, $"Nobody roamed near the 8 ceiling (farthest was {farthestReached.Max()}).");
        Assert.True(farthestReached.Min() < 4, $"Nobody stayed near the 3 floor (closest was {farthestReached.Min()}).");
    }

    [Fact]
    public void EveryPauseLastsBetweenThreeAndTenTicks()
    {
        // Counted before the first leg, where a person is provably standing still because
        // they haven't been given anywhere to go yet. Over enough people the drawn lengths
        // have to cover IdleTask's whole documented 3..10 band, endpoints included.
        var leadingStillTicks = Enumerable.Range(1, 200).Select(id =>
        {
            var start = new Position(0, 0);
            var person = new Person { Id = new PersonId(id), Name = $"Person {id}", BirthTick = 0, Position = start };
            var task = new IdleTask();
            var still = 0;
            while (still < 100)
            {
                task.Advance(person);
                if (person.Position != start)
                {
                    break;
                }

                still++;
            }

            return still;
        }).ToList();

        Assert.Equal(3, leadingStillTicks.Min());
        Assert.Equal(10, leadingStillTicks.Max());
    }

    [Fact]
    public void StandsStillBetweenLegsInsteadOfWalkingEveryTick()
    {
        var person = NewPerson(new Position(0, 0));
        var task = new IdleTask();
        var previous = person.Position;
        var stillTicks = 0;

        for (var i = 0; i < 600; i++)
        {
            task.Advance(person);
            if (person.Position == previous)
            {
                stillTicks++;
            }

            previous = person.Position;
        }

        // Without the pauses idle reads as restless, constant walking - a person on the move
        // every single one of 600 ticks.
        Assert.True(stillTicks > 50, $"Only {stillTicks} of 600 idle ticks were spent standing still.");
    }

    [Fact]
    public void SetsOffAgainAfterFinishingALegInsteadOfSettlingWhereItEnded()
    {
        var person = NewPerson(new Position(0, 0));
        var task = new IdleTask();
        var previous = person.Position;
        var movedLate = false;

        for (var i = 0; i < 600; i++)
        {
            task.Advance(person);
            if (i > 200 && person.Position != previous)
            {
                movedLate = true;
            }

            previous = person.Position;
        }

        Assert.True(movedLate, "The person stopped moving for good after an early leg.");
    }

    [Theory]
    [InlineData(1, 30, 0.02659296288065972, -0.6151410579074956)]
    [InlineData(1, 200, 1.082920373229459, 2.045077536888749)]
    [InlineData(2, 30, -2.346389077166879, -1.684520955564068)]
    [InlineData(2, 200, 0.12090731306143163, -1.044243761991774)]
    [InlineData(7, 30, 0.008717238678397035, -3.29998861743632)]
    [InlineData(7, 200, 0.24047159116368516, 0.6928283562976543)]
    public void APersonsWanderPathIsFixedByTheirId(int personId, int ticks, double expectedX, double expectedY)
    {
        // The seed avalanche, the per-person radius, the pause lengths and the destination
        // math together make one reproducible path per person - the property the whole class
        // is built around (nothing here is allowed to depend on wall-clock time or on the
        // order the simulation happens to advance people in). Pinned to six decimals rather
        // than exactly: the destinations come out of Math.Cos/Sin, whose last bit isn't
        // guaranteed identical across platforms.
        var person = new Person
        {
            Id = new PersonId(personId),
            Name = $"Person {personId}",
            BirthTick = 0,
            Position = new Position(0, 0),
        };
        var task = new IdleTask();

        for (var i = 0; i < ticks; i++)
        {
            task.Advance(person);
        }

        Assert.Equal(expectedX, person.Position.X, 6);
        Assert.Equal(expectedY, person.Position.Y, 6);
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
