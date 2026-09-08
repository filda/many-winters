namespace ManyWinters.Godot.Logic;

// A small set of objects too expensive to make one of per user, lent out one at a time and
// handed back when done. The hover rim is the case it exists for: there are thousands of
// sprites and at most one entity is ever hovered (HoverArbiter enforces exactly that), so a
// ShaderMaterial per sprite would be thousands of them to show a highlight one of them wears.
//
// Deliberately not a cap or a cache: it never evicts and never refuses. The number of objects
// it ends up holding is whatever the widest simultaneous use needed - four, for the widest
// entity's layers - and holding that few forever is cheaper than deciding when to let one go.
internal sealed class LendingPool<T>(Func<T> create)
    where T : class
{
    private readonly Stack<T> _idle = new();

    // How many are sitting unused right now - for tests, and for anyone wondering whether the
    // "at most one user" assumption above still holds.
    public int Idle => _idle.Count;

    public T Take() => _idle.Count > 0 ? _idle.Pop() : create();

    // Handing the same object back twice would lend it to two users at once, so callers give
    // one back exactly where they stop using it (HoverOutline.Clear, driven by losing hover).
    public void Return(T item) => _idle.Push(item);
}
