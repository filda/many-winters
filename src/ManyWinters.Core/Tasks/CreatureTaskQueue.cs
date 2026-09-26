using ManyWinters.Core.Population;

namespace ManyWinters.Core.Tasks;

public sealed class CreatureTaskQueue
{
    private readonly Queue<CreatureTask> _pending = new();

    public CreatureTask? Current { get; private set; }

    public void Enqueue(CreatureTask task) => _pending.Enqueue(task);

    // A new order preempts whatever the person was doing, rather than waiting behind it.
    public void Interrupt(CreatureTask task)
    {
        _pending.Clear();
        Current = task;
    }

    public void Advance(Creature creature)
    {
        Current?.Advance(creature);
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
