namespace ManyWinters.Godot.Logic;

// How much rendered time has piled up since the last simulation tick, and whether that is
// enough to owe another one. At most one tick fires per Advance call - a slow frame does not
// catch the simulation up all at once, it just leaves the remainder waiting for the next call
// (see SimulationLoop, which also decides not to call Advance at all while a modal holds the
// clock, so a held clock neither advances nor consumes what has already accumulated).
internal sealed class TickAccumulator(double intervalSeconds)
{
    private double _accumulated;

    // Adds delta and reports whether that is now enough for a tick. When it is, exactly one
    // interval is taken back out - never reset to zero - so a slow frame's extra time still
    // counts toward the next one instead of being thrown away.
    public bool Advance(double delta)
    {
        _accumulated += delta;
        if (_accumulated < intervalSeconds)
        {
            return false;
        }

        _accumulated -= intervalSeconds;
        return true;
    }

    // Makes the next Advance call tick regardless of how little time has passed - what letting
    // a clock-holding page go is supposed to do, so the world resumes on the very next frame
    // rather than up to a full interval later.
    public void TickAsSoonAsPossible() => _accumulated = intervalSeconds;
}
