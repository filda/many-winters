namespace OfFolk.Core.Tasks;

public sealed class PersonTaskQueue
{
    private readonly Queue<PersonTask> _pending = new();

    public PersonTask? Current { get; private set; }

    public void Enqueue(PersonTask task) => _pending.Enqueue(task);

    // Completion is decided by the task itself; this only ever pulls the next one once it says so.
    public void AdvanceIfComplete()
    {
        if (Current is null || Current.IsComplete)
        {
            Current = _pending.Count > 0 ? _pending.Dequeue() : null;
        }
    }
}
