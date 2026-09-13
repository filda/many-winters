using ManyWinters.Core.Continuity;

namespace ManyWinters.Godot.Logic;

// Decides when the inscription over a band's end goes up on screen: once per change of fate,
// never for a band whose line still goes on. The fate itself is read off the world every tick
// (BandEnding.FateOf) and says nothing about whether the player has been shown it yet - that
// is this one bit of presentation state, kept apart from the overlay so it can be tested
// without one.
internal sealed class EndingAnnouncements
{
    private BandFate _announced = BandFate.Living;

    // True on the first tick a fate differs from the last one announced, unless that fate is
    // Living: a line reopened (an NPC joining, one day) is silently noted, so that its
    // closing again is announced again, but nothing goes up on screen for good news.
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
