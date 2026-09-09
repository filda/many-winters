using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Population;

public class PersonTests
{
    [Fact]
    public void UnknownHasTheEmptyIdWhichNoEntityEverDraws()
    {
        Assert.Equal(new PersonId(Guid.Empty), Person.Unknown.Id);
    }

    [Fact]
    public void ANewPersonDrawsItsOwnIdDistinctFromEveryOther()
    {
        var first = new Person { Name = "Ava", BirthTick = 0, Mother = Person.Unknown, Father = Person.Unknown, Sex = TestPeople.AnySex };
        var second = new Person { Name = "Ava", BirthTick = 0, Mother = Person.Unknown, Father = Person.Unknown, Sex = TestPeople.AnySex };

        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(Person.Unknown.Id, first.Id);
    }

    [Fact]
    public void UnknownIsItsOwnMotherAndFatherSoEveryLineageTerminatesWithoutANull()
    {
        Assert.Same(Person.Unknown, Person.Unknown.Mother);
        Assert.Same(Person.Unknown, Person.Unknown.Father);
    }

    [Fact]
    public void UnknownIsLongDeadAndBuried()
    {
        Assert.False(Person.Unknown.IsAlive);
        Assert.True(Person.Unknown.IsBuried);
        Assert.Equal("Unknown", Person.Unknown.Name);
    }

    [Fact]
    public void UnknownIsOneSharedInstance()
    {
        Assert.Same(Person.Unknown, Person.Unknown);
    }
}
