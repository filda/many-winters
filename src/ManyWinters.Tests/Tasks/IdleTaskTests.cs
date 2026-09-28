using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Tasks;

public class IdleTaskTests
{
    private static Person NewPerson(Position position) =>
        new() { Id = TestIds.Person(1), Name = "Ava", BirthTick = 0, Position = position, Mother = Person.Unknown, Father = Person.Unknown, Sex = TestPeople.AnySex, Home = HomeAt(position) };

    // A camp of the rules' radius that never drifts, so a test about the walk itself has a still
    // anchor to walk around.
    private static HomeRange HomeAt(Position anchor) =>
        new(anchor) { Radius = SimulationRules.Default.CampHomeRadius, DriftMetresPerSeason = 0f };

    [Fact]
    public void IsNeverComplete()
    {
        var task = new IdleTask(HomeAt(new Position(0, 0)), 0.15f, 3, 10);

        Assert.False(task.IsComplete);
    }

    [Fact]
    public void AdvanceMovesThePersonInsteadOfLeavingThemFrozen()
    {
        var person = NewPerson(new Position(3, 4));
        var task = new IdleTask(person.Home, 0.15f, 3, 10);

        // Enough ticks to clear even the longest pre-leg pause.
        for (var i = 0; i < 20; i++)
        {
            task.Advance(person);
        }

        Assert.NotEqual(new Position(3, 4), person.Position);
    }

    [Fact]
    public void TwoDifferentPeopleWanderIndependently()
    {
        var start = new Position(3, 4);
        var ava = new Person { Id = TestIds.Person(1), Name = "Ava", BirthTick = 0, Position = start, Mother = Person.Unknown, Father = Person.Unknown, Sex = TestPeople.AnySex, Home = HomeAt(start) };
        var bran = new Person { Id = TestIds.Person(2), Name = "Bran", BirthTick = 0, Position = start, Mother = Person.Unknown, Father = Person.Unknown, Sex = TestPeople.AnySex, Home = HomeAt(start) };
        var avaTask = new IdleTask(ava.Home, 0.15f, 3, 10);
        var branTask = new IdleTask(bran.Home, 0.15f, 3, 10);

        // Clears the longest possible pre-leg pause for both.
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
        // Guards against seed avalanche: System.Random correlates badly on nearby small seeds
        // (sequential person ids), which would read as synchronized wandering.
        var start = new Position(0, 0);
        var ava = new Person { Id = TestIds.Person(1), Name = "Ava", BirthTick = 0, Position = start, Mother = Person.Unknown, Father = Person.Unknown, Sex = TestPeople.AnySex, Home = HomeAt(start) };
        var bran = new Person { Id = TestIds.Person(2), Name = "Bran", BirthTick = 0, Position = start, Mother = Person.Unknown, Father = Person.Unknown, Sex = TestPeople.AnySex, Home = HomeAt(start) };
        var avaTask = new IdleTask(ava.Home, 0.15f, 3, 10);
        var branTask = new IdleTask(bran.Home, 0.15f, 3, 10);

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
    public void EveryPauseLastsBetweenThreeAndTenTicks()
    {
        // Counted before the first leg, where a person is provably standing still. Over enough
        // people the pause lengths must cover IdleTask's whole 3..10 band, endpoints included.
        var leadingStillTicks = Enumerable.Range(1, 200).Select(id =>
        {
            var start = new Position(0, 0);
            var person = new Person { Id = TestIds.Person(id), Name = $"Person {id}", BirthTick = 0, Position = start, Mother = Person.Unknown, Father = Person.Unknown, Sex = TestPeople.AnySex, Home = HomeAt(start) };
            var task = new IdleTask(person.Home, 0.15f, 3, 10);
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
        var task = new IdleTask(person.Home, 0.15f, 3, 10);
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

        // Without the pauses idle reads as restless, constant walking.
        Assert.True(stillTicks > 50, $"Only {stillTicks} of 600 idle ticks were spent standing still.");
    }

    [Fact]
    public void SetsOffAgainAfterFinishingALegInsteadOfSettlingWhereItEnded()
    {
        var person = NewPerson(new Position(0, 0));
        var task = new IdleTask(person.Home, 0.15f, 3, 10);
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
    [InlineData(1, 30, -3.6213511232605313, -0.9738157729502984)]
    [InlineData(1, 200, -2.210144619947194, 4.066760615289442)]
    [InlineData(2, 30, -3.73571565563079, 1.5642665513999328)]
    [InlineData(2, 200, 4.691001742695548, -3.823501104844522)]
    [InlineData(7, 30, -0.068386825681177876, -3.2992914553783672)]
    [InlineData(7, 200, 1.3201271899270497, 4.938484952725375)]
    public void APersonsWanderPathIsFixedByTheirId(int personId, int ticks, double expectedX, double expectedY)
    {
        // One reproducible path per person is the property the class is built around. Pinned to
        // six decimals: the destinations come out of Math.Cos/Sin, whose last bit varies across
        // platforms.
        var person = new Person
        {
            Id = TestIds.Person(personId),
            Name = $"Person {personId}",
            BirthTick = 0,
            Position = new Position(0, 0),
            Mother = Person.Unknown,
            Father = Person.Unknown,
            Sex = TestPeople.AnySex,
            Home = HomeAt(new Position(0, 0)),
        };
        var task = new IdleTask(person.Home, 0.15f, 3, 10);

        for (var i = 0; i < ticks; i++)
        {
            task.Advance(person);
        }

        Assert.Equal(expectedX, person.Position.X, 6);
        Assert.Equal(expectedY, person.Position.Y, 6);
    }

    [Fact]
    public void WanderingStaysWithinTheHomesRadiusOfItsAnchor()
    {
        // A radius (6) other than the camp's own, so this could not pass by accident of the
        // person's own default home.
        var home = new HomeRange(new Position(100, 100)) { Radius = 6f, DriftMetresPerSeason = 0f };
        var person = NewPerson(home.Anchor);
        var task = new IdleTask(home, 0.15f, 3, 10);

        for (var i = 0; i < 500; i++)
        {
            task.Advance(person);
            Assert.True(WorldState.Distance(home.Anchor, person.Position) <= 6f + 0.01f);
        }
    }

    [Fact]
    public void TheAnchorIsReReadEveryLegSoWanderingFollowsItAsItDrifts()
    {
        const long ticksPerSeason = 75;
        var home = new HomeRange(new Position(0, 0)) { Radius = 5f, DriftMetresPerSeason = 50f };
        var person = NewPerson(home.Anchor);
        var task = new IdleTask(home, 0.15f, 3, 10);

        // Establishes which season "now" is without moving anything.
        home.Advance(0, ticksPerSeason);

        // A few legs near the original anchor, before it has moved anywhere.
        for (var i = 0; i < 20; i++)
        {
            task.Advance(person);
        }

        // One season turn moves the anchor 50m away - far outside the old wander disk.
        home.Advance(ticksPerSeason, ticksPerSeason);
        Assert.True(WorldState.Distance(new Position(0, 0), home.Anchor) > 10f, "The anchor did not actually move.");

        // Plenty of ticks to walk the 50m gap and settle back into its wander radius - each leg
        // reads home.Anchor fresh (see IdleTask), so it is drawn toward the new anchor rather
        // than the stale one it started near.
        for (var i = 0; i < 5000; i++)
        {
            task.Advance(person);
        }

        Assert.True(WorldState.Distance(home.Anchor, person.Position) <= 5f + 0.01f);
    }

    [Fact]
    public void TheSamePersonWandersTheSameWayFromAFreshTask()
    {
        var start = new Position(3, 4);
        var first = NewPerson(start);
        var second = NewPerson(start);

        new IdleTask(first.Home, 0.15f, 3, 10).Advance(first);
        new IdleTask(second.Home, 0.15f, 3, 10).Advance(second);

        Assert.Equal(first.Position, second.Position);
    }
}
