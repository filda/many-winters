using ManyWinters.Core.Continuity;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Continuity;

public class BandEndingTests
{
    // SimulationRules.Default: 75-tick seasons, Winter the fourth, so winters begin at ticks
    // 225, 525, 825, ... - the numbers every expectation below is counted against.
    private const long WinterBegins = 225;

    private static Person NewPerson(string name, Sex sex, long birthTick, int idSeed = 1) =>
        new()
        {
            Id = TestIds.Person(idSeed),
            Name = name,
            BirthTick = birthTick,
            Mother = Person.Unknown,
            Father = Person.Unknown,
            Sex = sex,
        };

    private static Person Dead(Person person, long deathTick, DeathCause cause = DeathCause.Hunger, bool buried = false)
    {
        person.IsAlive = false;
        person.DeathTick = deathTick;
        person.CauseOfDeath = cause;
        person.IsBuried = buried;
        return person;
    }

    private static WorldState WorldWith(params Person[] people)
    {
        var world = TestCatalogs.CreateWorld();
        foreach (var person in people)
        {
            world.AddPerson(person);
        }

        return world;
    }

    [Fact]
    public void ABandWithLivingMenAndWomenIsLiving()
    {
        var fate = BandEnding.FateOf([NewPerson("Sela", Sex.Female, 0), NewPerson("Doran", Sex.Male, 0)]);

        Assert.Equal(BandFate.Living, fate);
    }

    [Fact]
    public void WithNoLivingManTheSpearSideHasEnded()
    {
        var fate = BandEnding.FateOf([NewPerson("Sela", Sex.Female, 0), Dead(NewPerson("Doran", Sex.Male, 0), 10)]);

        Assert.Equal(BandFate.SpearSideEnded, fate);
    }

    [Fact]
    public void WithNoLivingWomanTheSpindleSideHasEnded()
    {
        var fate = BandEnding.FateOf([Dead(NewPerson("Sela", Sex.Female, 0), 10), NewPerson("Doran", Sex.Male, 0)]);

        Assert.Equal(BandFate.SpindleSideEnded, fate);
    }

    [Fact]
    public void WithNobodyLivingTheBandHasEnded()
    {
        var fate = BandEnding.FateOf([Dead(NewPerson("Sela", Sex.Female, 0), 10), Dead(NewPerson("Doran", Sex.Male, 0), 10)]);

        Assert.Equal(BandFate.Ended, fate);
    }

    [Fact]
    public void ALivingBandHasNoEndingToWriteAbout()
    {
        var world = WorldWith(NewPerson("Sela", Sex.Female, 0), NewPerson("Doran", Sex.Male, 0));

        Assert.Null(BandEnding.Of(world));
    }

    [Fact]
    public void AWorldWithNobodyInItHasNoBandToSpeakOf()
    {
        var world = TestCatalogs.CreateWorld();

        var error = Assert.Throws<ArgumentException>(() => BandEnding.Of(world));

        Assert.Contains("nobody in it", error.Message);
    }

    [Fact]
    public void AnEndedBandIsDescribedInFull()
    {
        var sela = NewPerson("Sela", Sex.Female, -600, idSeed: 1);
        var doran = NewPerson("Doran", Sex.Male, -300, idSeed: 2);
        var ava = NewPerson("Ava", Sex.Female, 400, idSeed: 3);
        var world = WorldWith(
            Dead(sela, 700, DeathCause.OldAge, buried: true),
            Dead(doran, 500, buried: true),
            Dead(ava, 1000));
        world.SpawnGrave(new Position(0, 0), isMarked: true, name: "Doran");
        world.SpawnGrave(new Position(1, 0), isMarked: false);

        var ending = BandEnding.Of(world);

        Assert.NotNull(ending);
        Assert.Equal(BandFate.Ended, ending.Fate);
        Assert.Equal("Sela's people", ending.BandName);
        Assert.Same(ava, ending.LastToDie);
        Assert.Equal(1000, ending.EndingTick);
        Assert.Equal(Season.Summer, ending.SeasonOfEnding);

        // Winters began at 225, 525 and 825 before the end at 1000.
        Assert.Equal(3, ending.WintersSeen);
        Assert.Equal(0, ending.Survivors);

        // Ava alone was born after the band arrived; Sela and Doran came with it.
        Assert.Equal(1, ending.Born);
        Assert.Equal(2, ending.Graves);
        Assert.Equal(1, ending.MarkedGraves);
        Assert.Equal(1, ending.Unburied);

        // The last man died at 500, the last woman at 1000: two winters (525, 825) in between.
        Assert.Equal(BandFate.SpearSideEnded, ending.SideThatEndedFirst);
        Assert.Equal(2, ending.WintersKeptAfterwards);
    }

    [Fact]
    public void ASpearSideEndingIsToldFromTheLastMansDeath()
    {
        var world = WorldWith(
            NewPerson("Sela", Sex.Female, -600),
            NewPerson("Tora", Sex.Female, -600),
            Dead(NewPerson("Doran", Sex.Male, -300, idSeed: 2), 500),
            Dead(NewPerson("Bran", Sex.Male, -300, idSeed: 3), 300));
        world.Clock.Advance(900);

        var ending = BandEnding.Of(world);

        Assert.NotNull(ending);
        Assert.Equal(BandFate.SpearSideEnded, ending.Fate);
        Assert.Equal("Doran", ending.LastToDie?.Name);
        Assert.Equal(500, ending.EndingTick);
        Assert.Equal(Season.Autumn, ending.SeasonOfEnding);
        Assert.Equal(1, ending.WintersSeen);
        Assert.Equal(2, ending.Survivors);
        Assert.Equal(2, ending.Unburied);
        Assert.Null(ending.SideThatEndedFirst);
        Assert.Equal(0, ending.WintersKeptAfterwards);
    }

    [Fact]
    public void ASpindleSideEndingIsToldFromTheLastWomansDeath()
    {
        var world = WorldWith(
            NewPerson("Doran", Sex.Male, -300),
            Dead(NewPerson("Sela", Sex.Female, -600, idSeed: 2), 800),
            Dead(NewPerson("Bran", Sex.Male, -300, idSeed: 3), 900));

        var ending = BandEnding.Of(world);

        Assert.NotNull(ending);
        Assert.Equal(BandFate.SpindleSideEnded, ending.Fate);
        Assert.Equal("Sela", ending.LastToDie?.Name);
        Assert.Equal(800, ending.EndingTick);
        Assert.Equal(1, ending.Survivors);
    }

    // A band that never had a man has no death to date the spear side's ending by: the line
    // was never open, and the ending is dated to now.
    [Fact]
    public void ALineNobodyWasEverOnHasNoLastDeath()
    {
        var world = WorldWith(NewPerson("Sela", Sex.Female, -600), NewPerson("Tora", Sex.Female, -600));
        world.Clock.Advance(250);

        var ending = BandEnding.Of(world);

        Assert.NotNull(ending);
        Assert.Equal(BandFate.SpearSideEnded, ending.Fate);
        Assert.Null(ending.LastToDie);
        Assert.Equal(250, ending.EndingTick);
        Assert.Equal(Season.Winter, ending.SeasonOfEnding);
        Assert.Equal(1, ending.WintersSeen);
    }

    // With no man ever, the spear side counts as closed from the band's arrival, so the women
    // "kept the fire" for every winter the band saw.
    [Fact]
    public void ABandOfWomenAloneKeptTheFireFromTheStart()
    {
        var world = WorldWith(
            Dead(NewPerson("Sela", Sex.Female, -600, idSeed: 1), 700),
            Dead(NewPerson("Tora", Sex.Female, -600, idSeed: 2), 600));

        var ending = BandEnding.Of(world);

        Assert.NotNull(ending);
        Assert.Equal(BandFate.Ended, ending.Fate);
        Assert.Equal(BandFate.SpearSideEnded, ending.SideThatEndedFirst);
        Assert.Equal(2, ending.WintersKeptAfterwards);
        Assert.Equal(2, ending.WintersSeen);
    }

    [Fact]
    public void WhenTheLastWomanDiesFirstTheMenKeptTheFire()
    {
        var world = WorldWith(
            Dead(NewPerson("Sela", Sex.Female, -600, idSeed: 1), 100),
            Dead(NewPerson("Doran", Sex.Male, -300, idSeed: 2), 600));

        var ending = BandEnding.Of(world);

        Assert.NotNull(ending);
        Assert.Equal(BandFate.SpindleSideEnded, ending.SideThatEndedFirst);
        Assert.Equal(2, ending.WintersKeptAfterwards);
    }

    [Fact]
    public void ALastCoupleDyingTogetherClosesBothSidesAtOnce()
    {
        var world = WorldWith(
            Dead(NewPerson("Sela", Sex.Female, -600, idSeed: 1), 600),
            Dead(NewPerson("Doran", Sex.Male, -300, idSeed: 2), 600));

        var ending = BandEnding.Of(world);

        Assert.NotNull(ending);
        Assert.Null(ending.SideThatEndedFirst);
        Assert.Equal(0, ending.WintersKeptAfterwards);
    }

    // The range of winters counted after a line closed starts the tick after the death: a
    // winter beginning that very next tick counts, one beginning on the death tick does not.
    [Fact]
    public void AWinterBeginningTheTickAfterTheLineClosedCounts()
    {
        var world = WorldWith(
            Dead(NewPerson("Doran", Sex.Male, -300, idSeed: 1), WinterBegins - 1),
            Dead(NewPerson("Sela", Sex.Female, -600, idSeed: 2), 300));

        var ending = BandEnding.Of(world);

        Assert.NotNull(ending);
        Assert.Equal(1, ending.WintersKeptAfterwards);
    }

    [Fact]
    public void AWinterBeginningOnTheDeathTickItselfDoesNotCountAsKeptAfterwards()
    {
        var world = WorldWith(
            Dead(NewPerson("Doran", Sex.Male, -300, idSeed: 1), 525),
            Dead(NewPerson("Sela", Sex.Female, -600, idSeed: 2), 1000));

        var ending = BandEnding.Of(world);

        Assert.NotNull(ending);

        // Only the winter of 825; the one that began at 525 was already under way.
        Assert.Equal(1, ending.WintersKeptAfterwards);
    }

    [Fact]
    public void ABandGoneBeforeItsFirstWinterSawNone()
    {
        var world = WorldWith(Dead(NewPerson("Sela", Sex.Female, -600), WinterBegins - 1));

        var ending = BandEnding.Of(world);

        Assert.NotNull(ending);
        Assert.Equal(0, ending.WintersSeen);
    }

    [Fact]
    public void TheWinterABandDiesInCounts()
    {
        var world = WorldWith(Dead(NewPerson("Sela", Sex.Female, -600), WinterBegins));

        var ending = BandEnding.Of(world);

        Assert.NotNull(ending);
        Assert.Equal(1, ending.WintersSeen);
    }

    [Fact]
    public void TwoDyingTheSameTickAreToldApartByName()
    {
        var world = WorldWith(
            Dead(NewPerson("Tora", Sex.Female, -600, idSeed: 1), 600),
            Dead(NewPerson("Ava", Sex.Female, -600, idSeed: 2), 600));

        var ending = BandEnding.Of(world);

        Assert.NotNull(ending);
        Assert.Equal("Ava", ending.LastToDie?.Name);
    }

    // A death nobody dated (a test fixture's, say) is taken as the most recent one, and the
    // ending is dated to now rather than to nothing.
    [Fact]
    public void AnUndatedDeathIsTakenAsTheLatestAndDatedToNow()
    {
        var undated = NewPerson("Ava", Sex.Female, -600, idSeed: 1);
        undated.IsAlive = false;
        var world = WorldWith(undated, Dead(NewPerson("Tora", Sex.Female, -600, idSeed: 2), 600));
        world.Clock.Advance(700);

        var ending = BandEnding.Of(world);

        Assert.NotNull(ending);
        Assert.Same(undated, ending.LastToDie);
        Assert.Equal(700, ending.EndingTick);
    }

    [Fact]
    public void SomeoneBornOnTheArrivalTickCameWithTheBand()
    {
        var world = WorldWith(Dead(NewPerson("Sela", Sex.Female, 0), 100));

        var ending = BandEnding.Of(world);

        Assert.NotNull(ending);
        Assert.Equal(0, ending.Born);
    }
}
