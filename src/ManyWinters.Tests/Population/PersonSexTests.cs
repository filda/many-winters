using ManyWinters.Core.Population;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Population;

public class PersonSexTests
{
    [Fact]
    public void APersonKeepsTheSexTheyWereGiven()
    {
        var person = new Person
        {
            Id = TestIds.Person(7),
            Name = "Ava",
            BirthTick = 0,
            Mother = Person.Unknown,
            Father = Person.Unknown,
            Sex = Sex.Male,
        };

        Assert.Equal(Sex.Male, person.Sex);
    }

    // Person.Sex is required, so a caller with no opinion has to ask for a draw out loud, as
    // SpawnPersonCommand and BirthCommand do; nothing derives one silently.
    [Fact]
    public void SexOfDrawsOneForACallerWithNoOpinion()
    {
        Assert.Contains(Person.SexOf(TestIds.Person(7)), new[] { Sex.Female, Sex.Male });
    }

    [Fact]
    public void TheSameIdAlwaysDrawsTheSameSex()
    {
        // MapLoader regenerates its starting band from a fixed seed rather than loading it, so
        // a draw that moved would make a new game a different world.
        Assert.Equal(Person.SexOf(TestIds.Person(7)), Person.SexOf(TestIds.Person(7)));
    }

    // Why SexOf runs its seed through SeedHash: consecutive ids must not alternate or all agree.
    [Fact]
    public void NeighbouringIdsDoNotAllDrawTheSameSex()
    {
        var sexes = Enumerable.Range(1, 40).Select(seed => Person.SexOf(TestIds.Person(seed))).ToList();

        Assert.Contains(Sex.Female, sexes);
        Assert.Contains(Sex.Male, sexes);
    }

    [Fact]
    public void BothSexesTurnUpRoughlyEquallyOftenAcrossManyIds()
    {
        var females = Enumerable.Range(1, 1000).Count(seed => Person.SexOf(TestIds.Person(seed)) == Sex.Female);

        Assert.InRange(females, 400, 600);
    }
}
