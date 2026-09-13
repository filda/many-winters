using ManyWinters.Core.Continuity;
using ManyWinters.Core.Population;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Continuity;

public class BandNameTests
{
    private static Person NewPerson(string name, long birthTick, bool isAlive = true, int idSeed = 1) =>
        new()
        {
            Id = TestIds.Person(idSeed),
            Name = name,
            BirthTick = birthTick,
            IsAlive = isAlive,
            Mother = Person.Unknown,
            Father = Person.Unknown,
            Sex = TestPeople.AnySex,
        };

    [Fact]
    public void ABandIsCalledAfterItsEldestMember()
    {
        var name = BandName.Of([NewPerson("Ava", -300), NewPerson("Liska", -2700), NewPerson("Bran", -600)]);

        Assert.Equal("Liska's people", name);
    }

    // The eldest ever, not the eldest still standing: the name is settled when the band exists
    // and does not move on to the next-oldest every time somebody dies.
    [Fact]
    public void TheEldestKeepsTheNameAfterDying()
    {
        var name = BandName.Of([NewPerson("Ava", -300), NewPerson("Liska", -2700, isAlive: false)]);

        Assert.Equal("Liska's people", name);
    }

    [Fact]
    public void TwoBornTheSameTickAreToldApartByName()
    {
        var name = BandName.Of([NewPerson("Tora", -600), NewPerson("Bran", -600)]);

        Assert.Equal("Bran's people", name);
        Assert.Equal(name, BandName.Of([NewPerson("Bran", -600), NewPerson("Tora", -600)]));
    }

    [Fact]
    public void TwoWithTheSameNameAndBirthAreToldApartById()
    {
        var first = NewPerson("Bran", -600, idSeed: 1);
        var second = NewPerson("Bran", -600, idSeed: 2);

        // The same answer either way round is the point; which id wins is the tie-break's own
        // business, but it must not depend on list order.
        Assert.Equal(BandName.Of([first, second]), BandName.Of([second, first]));
    }
}
