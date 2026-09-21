using ManyWinters.Core.Continuity;

namespace ManyWinters.Godot.Logic;

// When the inscription over a band's end goes up: once per change of fate, never for a band
// whose line still goes on. The fate is read off the world every tick and says nothing about
// whether the player has seen it; that one bit of state lives here, apart from the overlay, so
// it can be tested without one.
internal sealed class EndingAnnouncements
{
    private BandFate _announced = BandFate.Living;

    // True on the first tick a fate differs from the last announced, unless it is Living: a
    // line reopened is noted silently so its closing again is announced again, but nothing
    // goes up for good news.
    public bool ShouldAnnounce(BandFate fate)
    {
        if (fate == _announced)
        {
            return false;
        }

        _announced = fate;
        return fate != BandFate.Living;
    }
}
