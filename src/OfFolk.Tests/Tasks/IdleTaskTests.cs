using OfFolk.Core.Tasks;

namespace OfFolk.Tests.Tasks;

public class IdleTaskTests
{
    [Fact]
    public void IsNeverComplete()
    {
        var task = new IdleTask();

        Assert.False(task.IsComplete);
    }
}
