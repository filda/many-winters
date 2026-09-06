using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.Population;

public class PersonTests
{
    [Fact]
    public void UnknownHasIdZeroWhichNoWorldEverHandsOut()
    {
        Assert.Equal(new PersonId(0), Person.Unknown.Id);
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
