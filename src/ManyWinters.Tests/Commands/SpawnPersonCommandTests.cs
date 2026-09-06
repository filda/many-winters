using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class SpawnPersonCommandTests
{
    [Fact]
    public void ExecuteAddsAPersonAtTheGivenPosition()
    {
        var world = TestCatalogs.CreateWorld();

        world.Execute(new SpawnPersonCommand("Ava", new Position(3, 4), Person.Unknown, Person.Unknown));

        var person = Assert.Single(world.People);
        Assert.Equal("Ava", person.Name);
        Assert.Equal(new Position(3, 4), person.Position);
    }

    [Fact]
    public void ExecuteWithAnInitialAgeBackdatesTheBirthTick()
    {
        var world = TestCatalogs.CreateWorld();
        world.Clock.Advance(1000);

        world.Execute(new SpawnPersonCommand("Ava", new Position(0, 0), Person.Unknown, Person.Unknown, initialAgeTicks: 300));

        var person = Assert.Single(world.People);
        Assert.Equal(700, person.BirthTick);
    }

    [Fact]
    public void ExecuteWithoutAnInitialAgeUsesTheCurrentTickAsTheBirthTick()
    {
        var world = TestCatalogs.CreateWorld();
        world.Clock.Advance(1000);

        world.Execute(new SpawnPersonCommand("Ava", new Position(0, 0), Person.Unknown, Person.Unknown));

        var person = Assert.Single(world.People);
        Assert.Equal(1000, person.BirthTick);
    }

    [Fact]
    public void ExecuteWiresTheGivenMotherAndFather()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = world.SpawnPerson("Sela", new Position(0, 0));
        var father = world.SpawnPerson("Bran", new Position(0, 0));

        world.Execute(new SpawnPersonCommand("Ava", new Position(0, 0), mother, father));

        var child = world.People.Single(p => p.Name == "Ava");
        Assert.Same(mother, child.Mother);
        Assert.Same(father, child.Father);
    }

    [Fact]
    public void ExecutingTwiceAddsTwoDistinctPeople()
    {
        var world = TestCatalogs.CreateWorld();

        world.Execute(new SpawnPersonCommand("Ava", new Position(0, 0), Person.Unknown, Person.Unknown));
        world.Execute(new SpawnPersonCommand("Bran", new Position(1, 1), Person.Unknown, Person.Unknown));

        Assert.Equal(2, world.People.Count);
        Assert.NotEqual(world.People[0].Id, world.People[1].Id);
    }
}
