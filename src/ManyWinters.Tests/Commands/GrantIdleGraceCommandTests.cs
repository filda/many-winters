using ManyWinters.Core.Commands;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class GrantIdleGraceCommandTests
{
    [Fact]
    public void GrantingGraceSetsTheTickBeforeWhichThePersonWontBeMadeToWander()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));

        world.Execute(new GrantIdleGraceCommand(person.Id, 5));

        Assert.Equal(5, person.IdleGraceUntilTick);
    }

    [Fact]
    public void GrantingGraceAgainLaterMovesTheDeadlineForwardFromTheCurrentTick()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        world.Advance(10);

        world.Execute(new GrantIdleGraceCommand(person.Id, 5));

        Assert.Equal(15, person.IdleGraceUntilTick);
    }

    [Fact]
    public void GrantingGraceToADeadPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.IsAlive = false;

        world.Execute(new GrantIdleGraceCommand(person.Id, 5));

        Assert.Equal(0, person.IdleGraceUntilTick);
    }

    [Fact]
    public void GrantingGraceToAnUnknownPersonDoesNotThrow()
    {
        var world = TestCatalogs.CreateWorld();

        world.Execute(new GrantIdleGraceCommand(new PersonId(999), 5));
    }
}
