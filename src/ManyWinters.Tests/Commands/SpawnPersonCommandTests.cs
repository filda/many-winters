using ManyWinters.Core.Commands;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.Commands;

public class SpawnPersonCommandTests
{
    [Fact]
    public void ExecuteAddsAPersonAtTheGivenPosition()
    {
        var world = new WorldState();

        world.Execute(new SpawnPersonCommand("Ava", new Position(3, 4)));

        var person = Assert.Single(world.People);
        Assert.Equal("Ava", person.Name);
        Assert.Equal(new Position(3, 4), person.Position);
    }

    [Fact]
    public void ExecuteWithAnInitialAgeBackdatesTheBirthTick()
    {
        var world = new WorldState();
        world.Clock.Advance(1000);

        world.Execute(new SpawnPersonCommand("Ava", new Position(0, 0), InitialAgeTicks: 300));

        var person = Assert.Single(world.People);
        Assert.Equal(700, person.BirthTick);
    }

    [Fact]
    public void ExecuteWithoutAnInitialAgeUsesTheCurrentTickAsTheBirthTick()
    {
        var world = new WorldState();
        world.Clock.Advance(1000);

        world.Execute(new SpawnPersonCommand("Ava", new Position(0, 0)));

        var person = Assert.Single(world.People);
        Assert.Equal(1000, person.BirthTick);
    }

    [Fact]
    public void ExecutingTwiceAddsTwoDistinctPeople()
    {
        var world = new WorldState();

        world.Execute(new SpawnPersonCommand("Ava", new Position(0, 0)));
        world.Execute(new SpawnPersonCommand("Bran", new Position(1, 1)));

        Assert.Equal(2, world.People.Count);
        Assert.NotEqual(world.People[0].Id, world.People[1].Id);
    }
}
