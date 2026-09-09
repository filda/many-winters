using ManyWinters.Core.Population;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Population;

public class KinshipTests
{
    private static Person NewPerson(string name, Person? mother = null, Person? father = null) =>
        new()
        {
            Name = name,
            BirthTick = 0,
            Mother = mother ?? Person.Unknown,
            Father = father ?? Person.Unknown,
            Sex = TestPeople.AnySex,
        };

    [Fact]
    public void TwoStrangersAreNotKin()
    {
        Assert.False(Kinship.AreCloseKin(NewPerson("Ava"), NewPerson("Bran")));
    }

    [Fact]
    public void AMotherIsKinToHerChild()
    {
        var mother = NewPerson("Sela");
        var child = NewPerson("Ava", mother: mother);

        Assert.True(Kinship.AreCloseKin(mother, child));
    }

    [Fact]
    public void AFatherIsKinToHisChild()
    {
        var father = NewPerson("Doran");
        var child = NewPerson("Ava", father: father);

        Assert.True(Kinship.AreCloseKin(father, child));
    }

    // Which of the two is asked about first must not change the answer.
    [Fact]
    public void KinshipReadsTheSameFromEitherSide()
    {
        var mother = NewPerson("Sela");
        var child = NewPerson("Ava", mother: mother);

        Assert.True(Kinship.AreCloseKin(child, mother));
    }

    [Fact]
    public void FullSiblingsAreKin()
    {
        var mother = NewPerson("Sela");
        var father = NewPerson("Doran");
        var first = NewPerson("Ava", mother: mother, father: father);
        var second = NewPerson("Bran", mother: mother, father: father);

        Assert.True(Kinship.AreCloseKin(first, second));
    }

    [Fact]
    public void HalfSiblingsByTheMotherAreKin()
    {
        var mother = NewPerson("Sela");
        var first = NewPerson("Ava", mother: mother, father: NewPerson("Doran"));
        var second = NewPerson("Bran", mother: mother, father: NewPerson("Rask"));

        Assert.True(Kinship.AreCloseKin(first, second));
    }

    [Fact]
    public void HalfSiblingsByTheFatherAreKin()
    {
        var father = NewPerson("Doran");
        var first = NewPerson("Ava", mother: NewPerson("Sela"), father: father);
        var second = NewPerson("Bran", mother: NewPerson("Tora"), father: father);

        Assert.True(Kinship.AreCloseKin(first, second));
    }

    // The case that would otherwise make the whole starting band one family: Person.Unknown is
    // where every unrecorded line ends up, so sharing it is sharing nothing.
    [Fact]
    public void TwoPeopleWhoseParentsAreBothUnrecordedAreNotSiblings()
    {
        Assert.False(Kinship.AreCloseKin(NewPerson("Ava"), NewPerson("Bran")));
        Assert.Same(Person.Unknown, NewPerson("Ava").Mother);
    }

    [Fact]
    public void SomeoneWithOneRecordedParentIsNotKinToAStrangerWithNone()
    {
        var withMother = NewPerson("Ava", mother: NewPerson("Sela"));

        Assert.False(Kinship.AreCloseKin(withMother, NewPerson("Bran")));
    }

    // One generation each way is as far as this sees, and deliberately so - a Person names its
    // own mother and father and nothing beyond them.
    [Fact]
    public void CousinsAreNotCloseKin()
    {
        var grandmother = NewPerson("Orla");
        var firstParent = NewPerson("Sela", mother: grandmother);
        var secondParent = NewPerson("Tora", mother: grandmother);
        var firstCousin = NewPerson("Ava", mother: firstParent);
        var secondCousin = NewPerson("Bran", mother: secondParent);

        Assert.False(Kinship.AreCloseKin(firstCousin, secondCousin));
    }

    [Fact]
    public void AGrandmotherIsNotCloseKinToHerGrandchild()
    {
        var grandmother = NewPerson("Orla");
        var mother = NewPerson("Sela", mother: grandmother);
        var child = NewPerson("Ava", mother: mother);

        Assert.False(Kinship.AreCloseKin(grandmother, child));
    }
}
