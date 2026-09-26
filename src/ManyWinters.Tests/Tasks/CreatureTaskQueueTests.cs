using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Tasks;

public class CreatureTaskQueueTests
{
    private sealed class CompletableTask : CreatureTask
    {
        public bool Completed { get; set; }

        public Creature? AdvancedWith { get; private set; }

        public override bool IsComplete => Completed;

        public override void Advance(Creature creature) => AdvancedWith = creature;
    }

    private static Person NewPerson() => new() { Name = "Ava", BirthTick = 0, Mother = Person.Unknown, Father = Person.Unknown, Sex = TestPeople.AnySex };

    [Fact]
    public void NewQueueHasNoCurrentTask()
    {
        var queue = new CreatureTaskQueue();

        Assert.Null(queue.Current);
    }

    [Fact]
    public void AdvanceIfCompleteOnEmptyQueueLeavesCurrentNull()
    {
        var queue = new CreatureTaskQueue();

        queue.AdvanceIfComplete();

        Assert.Null(queue.Current);
    }

    [Fact]
    public void AdvanceIfCompletePullsNextTaskWhenNoneIsCurrent()
    {
        var queue = new CreatureTaskQueue();
        var task = new IdleTask();
        queue.Enqueue(task);

        queue.AdvanceIfComplete();

        Assert.Same(task, queue.Current);
    }

    [Fact]
    public void AdvanceIfCompleteKeepsCurrentTaskWhileIncomplete()
    {
        var queue = new CreatureTaskQueue();
        var task = new CompletableTask();
        queue.Enqueue(task);
        queue.AdvanceIfComplete();

        queue.AdvanceIfComplete();

        Assert.Same(task, queue.Current);
    }

    [Fact]
    public void AdvanceIfCompleteMovesToNextTaskOnceCurrentIsComplete()
    {
        var queue = new CreatureTaskQueue();
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
        var queue = new CreatureTaskQueue();
        var task = new IdleTask();

        queue.Interrupt(task);

        Assert.Same(task, queue.Current);
    }

    [Fact]
    public void InterruptReplacesWhicheverTaskWasAlreadyCurrent()
    {
        var queue = new CreatureTaskQueue();
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
        var queue = new CreatureTaskQueue();
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
        var queue = new CreatureTaskQueue();
        var task = new CompletableTask();
        queue.Interrupt(task);
        var person = NewPerson();

        queue.Advance(person);

        Assert.Same(person, task.AdvancedWith);
    }

    [Fact]
    public void AdvanceMovesToTheNextTaskOnceTheCurrentOneCompletes()
    {
        var queue = new CreatureTaskQueue();
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
        var queue = new CreatureTaskQueue();

        queue.Advance(NewPerson());
    }
}
