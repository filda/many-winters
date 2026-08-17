using OfFolk.Core.World;

namespace OfFolk.Tests.World;

public class WorldStateTests
{
    [Fact]
    public void AddPersonAssignsSequentialUniqueIds()
    {
        var world = new WorldState();

        var first = world.AddPerson("Ava", new Position(0, 0));
        var second = world.AddPerson("Bran", new Position(1, 1));

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(1, first.Id.Value);
        Assert.Equal(2, second.Id.Value);
    }

    [Fact]
    public void AddPersonTracksThemInPeople()
    {
        var world = new WorldState();

        world.AddPerson("Ava", new Position(0, 0));
        world.AddPerson("Bran", new Position(1, 1));

        Assert.Equal(2, world.People.Count);
    }

    [Fact]
    public void NewWorldHasNoPeopleAndTickZero()
    {
        var world = new WorldState();

        Assert.Empty(world.People);
        Assert.Equal(0, world.Clock.CurrentTick);
    }
}
