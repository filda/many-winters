namespace ManyWinters.Godot.Logic;

// Anything the cursor can light up. Free of engine types so it can live in Logic/ and be unit
// tested; "is the cursor still on me" is the implementor's job, since only it knows which
// sprite layers and texture the answer depends on.
internal interface IHoverable
{
    void ShowHovered(bool hovered);

    bool IsStillUnderCursor();
}

// One cursor, so at most one thing is hovered - and this owns that invariant instead of each
// view tracking its own flag. Godot's mouse_exited only reaches the collider its picking chose,
// so it never arrives for a view lit through a rescue path, and picking runs on mouse movement,
// so a person walking out from under a resting cursor is never told either. Revalidate, called
// once a frame, asks the lit view whether the cursor is still on it and depends on no engine
// event.
internal sealed class HoverArbiter
{
    private IHoverable? _hovered;

    public void Set(IHoverable target, bool hovered)
    {
        if (!hovered)
        {
            // Only the view actually lit has anything to turn off; clearing here otherwise
            // would drop someone else's hover.
            if (ReferenceEquals(_hovered, target))
            {
                Clear();
            }

            return;
        }

        if (ReferenceEquals(_hovered, target))
        {
            return;
        }

        Clear();
        _hovered = target;
        target.ShowHovered(true);
    }

    public void Clear()
    {
        if (_hovered is not { } previous)
        {
            return;
        }

        // Cleared before the call: ShowHovered may route back here without finding itself
        // still listed as current.
        _hovered = null;
        previous.ShowHovered(false);
    }

    public void Revalidate()
    {
        if (_hovered is { } hovered && !hovered.IsStillUnderCursor())
        {
            Clear();
        }
    }

    // A view leaving the scene drops out without being called back: QueueFree is already
    // pending, and touching a freed node crashes.
    public void Forget(IHoverable target)
    {
        if (ReferenceEquals(_hovered, target))
        {
            _hovered = null;
        }
    }
}
