namespace ManyWinters.Godot.Logic;

// A few objects too expensive to make one of per user, lent out one at a time and handed back.
// The hover rim is the case: thousands of sprites, at most one entity hovered at a time, so a
// ShaderMaterial per sprite would be thousands for a highlight one of them wears.
// Not a cap or a cache: it never evicts and never refuses. It ends up holding whatever the
// widest simultaneous use needed (four, for the widest entity's layers), which is cheaper
// than deciding when to let one go.
internal sealed class LendingPool<T>(Func<T> create)
    where T : class
{
    private readonly Stack<T> _idle = new();

    // How many sit unused right now - for tests, and for checking the "at most one user"
    // assumption above.
    public int Idle => _idle.Count;

    public T Take() => _idle.Count > 0 ? _idle.Pop() : create();

    // Returning the same object twice would lend it to two users at once, so callers hand one
    // back exactly where they stop using it - on losing hover.
    public void Return(T item) => _idle.Push(item);
}
