using ManyWinters.Core.Commands;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class MoveCommandTests
{
    [Fact]
    public void SetsTheSelectedPersonsCurrentTaskToAMoveTaskTowardTheDestination()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));

        world.Execute(new MoveCommand(person.Id, new Position(5, 5)));

        var task = Assert.IsType<MoveTask>(person.Tasks.Current);
        Assert.Equal(new Position(5, 5), task.Destination);
    }

    [Fact]
    public void InterruptsWhateverThePersonWasPreviouslyDoing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var previousTask = new IdleTask();
        person.Tasks.Interrupt(previousTask);

        world.Execute(new MoveCommand(person.Id, new Position(5, 5)));

        Assert.NotSame(previousTask, person.Tasks.Current);
        Assert.IsType<MoveTask>(person.Tasks.Current);
    }

    [Fact]
    public void RequiresTheMovingPersonToBeAlive()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.IsAlive = false;

        world.Execute(new MoveCommand(person.Id, new Position(5, 5)));

        Assert.Null(person.Tasks.Current);
    }

    [Fact]
    public void UnknownPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();

        world.Execute(new MoveCommand(new PersonId(999), new Position(5, 5)));
    }
}
