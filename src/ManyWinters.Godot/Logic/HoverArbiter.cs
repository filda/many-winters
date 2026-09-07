namespace ManyWinters.Godot.Logic;

// Anything the cursor can light up - a person, a resource node. Deliberately free of engine
// types (so this file can live in Logic/ and be unit-tested at all): asking "is the cursor
// still on me" is the implementor's own job, since only it knows which sprite layers and
// which texture the answer depends on.
internal interface IHoverable
{
    void ShowHovered(bool hovered);

    bool IsStillUnderCursor();
}

// There is one cursor, so at most one thing can be hovered - but nothing in the scene used to
// enforce that. Each view tracked its own hover flag and cleared it on Godot's own
// MouseExited, which only ever reaches the single collider Godot's picking chose. Two ways
// that left a sprite lit forever, both of them everyday (the stuck-highlight bug in
// docs/todo/todo.md - a crowd of people still tinted yellow long after the cursor left them):
//
//   - A view highlighted through HoverRescue - the cursor is genuinely over its opaque pixels,
//     but some other entity's broad-phase box won the pick - never received Godot's
//     mouse_entered either, so no mouse_exited was ever coming for it.
//   - A person who simply walked out from under a resting cursor. Picking is driven by mouse
//     movement; the mouse never moved, so nothing told them they were no longer under it.
//
// Both are fixed by taking the invariant away from the individual views: this owns the single
// hovered target (handing over clears the previous one), and Revalidate - called once a frame,
// see Main._Process - asks whoever is currently lit whether the cursor is still on them, which
// no longer depends on an engine event arriving at all.
internal sealed class HoverArbiter
{
    private IHoverable? _hovered;

    public void Set(IHoverable target, bool hovered)
    {
        if (!hovered)
        {
            // Only if it is the one actually showing the highlight: a view that isn't current
            // has nothing to turn off, and clearing here would drop someone else's hover.
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

        // Cleared before the call, not after: ShowHovered is free to route back here (a view
        // reacting to losing hover) without finding itself still listed as current.
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

    // A view leaving the scene (a felled tree, a removed person) drops out of here without
    // being called back into - QueueFree has already been asked for, and touching a freed
    // node is a crash rather than a stale highlight.
    public void Forget(IHoverable target)
    {
        if (ReferenceEquals(_hovered, target))
        {
            _hovered = null;
        }
    }
}
