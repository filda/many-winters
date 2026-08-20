using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.Tasks;

public class PersonTaskQueueTests
{
    private sealed class CompletableTask : PersonTask
    {
        public bool Completed { get; set; }

        public Person? AdvancedWith { get; private set; }

        public override bool IsComplete => Completed;

        public override void Advance(Person person) => AdvancedWith = person;
    }

    private static Person NewPerson() => new() { Id = new PersonId(1), Name = "Ava", BirthTick = 0 };

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

    [Fact]
    public void InterruptSetsTheGivenTaskAsCurrentImmediately()
    {
        var queue = new PersonTaskQueue();
        var task = new IdleTask();

        queue.Interrupt(task);

        Assert.Same(task, queue.Current);
    }

    [Fact]
    public void InterruptReplacesWhicheverTaskWasAlreadyCurrent()
    {
        var queue = new PersonTaskQueue();
        var first = new IdleTask();
        queue.Enqueue(first);
        queue.AdvanceIfComplete();
        var replacement = new IdleTask();

        queue.Interrupt(replacement);

        Assert.Same(replacement, queue.Current);
    }

    [Fact]
    public void InterruptDiscardsAnyPendingTasks()
    {
        var queue = new PersonTaskQueue();
        var stalePending = new CompletableTask { Completed = true };
        queue.Enqueue(stalePending);
        var interrupting = new CompletableTask { Completed = true };

        queue.Interrupt(interrupting);
        queue.AdvanceIfComplete();

        Assert.Null(queue.Current);
    }

    [Fact]
    public void AdvanceInvokesTheCurrentTasksAdvanceWithTheGivenPerson()
    {
        var queue = new PersonTaskQueue();
        var task = new CompletableTask();
        queue.Interrupt(task);
        var person = NewPerson();

        queue.Advance(person);

        Assert.Same(person, task.AdvancedWith);
    }

    [Fact]
    public void AdvanceMovesToTheNextTaskOnceTheCurrentOneCompletes()
    {
        var queue = new PersonTaskQueue();
        var first = new CompletableTask { Completed = true };
        var second = new IdleTask();
        queue.Interrupt(first);
        queue.Enqueue(second);

        queue.Advance(NewPerson());

        Assert.Same(second, queue.Current);
    }

    [Fact]
    public void AdvanceOnAnEmptyQueueDoesNotThrow()
    {
        var queue = new PersonTaskQueue();

        queue.Advance(NewPerson());
    }
}
