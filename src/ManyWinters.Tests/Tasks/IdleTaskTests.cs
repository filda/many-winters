using ManyWinters.Core.Tasks;

namespace ManyWinters.Tests.Tasks;

public class IdleTaskTests
{
    [Fact]
    public void IsNeverComplete()
    {
        var task = new IdleTask();

        Assert.False(task.IsComplete);
    }
}
