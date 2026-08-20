using ManyWinters.Core.Population;

namespace ManyWinters.Core.Tasks;

public sealed class PersonTaskQueue
{
    private readonly Queue<PersonTask> _pending = new();

    public PersonTask? Current { get; private set; }

    public void Enqueue(PersonTask task) => _pending.Enqueue(task);

    // A new order preempts whatever the person was doing, rather than waiting behind it.
    public void Interrupt(PersonTask task)
    {
        _pending.Clear();
        Current = task;
    }

    public void Advance(Person person)
    {
        Current?.Advance(person);
        AdvanceIfComplete();
    }

    // Completion is decided by the task itself; this only ever pulls the next one once it says so.
    public void AdvanceIfComplete()
    {
        if (Current is null || Current.IsComplete)
        {
            Current = _pending.Count > 0 ? _pending.Dequeue() : null;
        }
    }
}
