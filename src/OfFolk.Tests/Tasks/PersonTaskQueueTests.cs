using OfFolk.Core.Tasks;

namespace OfFolk.Tests.Tasks;

public class PersonTaskQueueTests
{
    private sealed class CompletableTask : PersonTask
    {
        public bool Completed { get; set; }

        public override bool IsComplete => Completed;
    }

    [Fact]
    public void NewQueueHasNoCurrentTask()
    {
        var queue = new PersonTaskQueue();

        Assert.Null(queue.Current);
    }

    [Fact]
    public void AdvanceIfCompleteOnEmptyQueueLeavesCurrentNull()
    {
        var queue = new PersonTaskQueue();

        queue.AdvanceIfComplete();

        Assert.Null(queue.Current);
    }

    [Fact]
    public void AdvanceIfCompletePullsNextTaskWhenNoneIsCurrent()
    {
        var queue = new PersonTaskQueue();
        var task = new IdleTask();
        queue.Enqueue(task);

        queue.AdvanceIfComplete();

        Assert.Same(task, queue.Current);
    }

    [Fact]
    public void AdvanceIfCompleteKeepsCurrentTaskWhileIncomplete()
    {
        var queue = new PersonTaskQueue();
        var task = new CompletableTask();
        queue.Enqueue(task);
        queue.AdvanceIfComplete();

        queue.AdvanceIfComplete();

        Assert.Same(task, queue.Current);
    }

    [Fact]
    public void AdvanceIfCompleteMovesToNextTaskOnceCurrentIsComplete()
    {
        var queue = new PersonTaskQueue();
        var first = new CompletableTask();
        var second = new IdleTask();
        queue.Enqueue(first);
        queue.Enqueue(second);
        queue.AdvanceIfComplete();

        first.Completed = true;
        queue.AdvanceIfComplete();

        Assert.Same(second, queue.Current);
    }
}
