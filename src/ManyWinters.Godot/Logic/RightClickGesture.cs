using Godot;

namespace ManyWinters.Godot.Logic;

// Telling a right-click apart from a right-drag. The right button does two jobs: held and
// dragged it turns the camera, pressed and released in one spot it asks what can be done with
// whatever is under the cursor. So the menu waits for the button to come up and
// opens only if the cursor stayed where it went down - otherwise every look around the camp
// would end in a menu.
//
// A few pixels of slack rather than none: a mouse drifts under a real finger, and a menu that
// refuses to open half the time is worse than one that occasionally opens after a nudge.
internal sealed class RightClickGesture
{
    internal const float DragThresholdPixels = 4f;

    private Vector2 _pressedAt;
    private bool _pressed;
    private bool _dragged;

    internal void Press(Vector2 screenPosition)
    {
        _pressedAt = screenPosition;
        _pressed = true;
        _dragged = false;
    }

    // Fed every mouse motion, pressed or not: motion with the button up is nobody's business
    // here, and checking the flag first is the same work as ignoring it.
    internal void Moved(Vector2 screenPosition)
    {
        if (_pressed && screenPosition.DistanceTo(_pressedAt) > DragThresholdPixels)
        {
            _dragged = true;
        }
    }

    // Whether the button coming up completes a click. Resets either way, so a release nobody
    // asked about cannot answer true later.
    internal bool Release()
    {
        var wasClick = _pressed && !_dragged;
        _pressed = false;
        // Stryker disable once Boolean: _dragged is only ever read guarded by "_pressed &&"
        // above, and Press() resets it before any later read - whatever it is reset to here is
        // never observed.
        _dragged = false;
        return wasClick;
    }
}
