using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.Tasks;

public class IdleTaskTests
{
    [Fact]
    public void IsNeverComplete()
    {
        var task = new IdleTask();

        Assert.False(task.IsComplete);
    }

    [Fact]
    public void AdvanceDoesNotChangeThePerson()
    {
        var person = new Person { Id = new PersonId(1), Name = "Ava", BirthTick = 0, Position = new Position(3, 4) };
        var task = new IdleTask();

        task.Advance(person);

        Assert.Equal(new Position(3, 4), person.Position);
    }
}
