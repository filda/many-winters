using ManyWinters.Core.Continuity;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

public class EndingAnnouncementsTests
{
    [Fact]
    public void ALivingBandIsNeverAnnounced()
    {
        var announcements = new EndingAnnouncements();

        Assert.False(announcements.ShouldAnnounce(BandFate.Living));
        Assert.False(announcements.ShouldAnnounce(BandFate.Living));
    }

    [Fact]
    public void AnEndedLineIsAnnouncedOnceAndThenLeftAlone()
    {
        var announcements = new EndingAnnouncements();

        Assert.True(announcements.ShouldAnnounce(BandFate.SpearSideEnded));
        Assert.False(announcements.ShouldAnnounce(BandFate.SpearSideEnded));
        Assert.False(announcements.ShouldAnnounce(BandFate.SpearSideEnded));
    }

    // The spear side closes first, and the last death is its own, second announcement.
    [Fact]
    public void TheLastDeathIsAnnouncedAfterTheLineEndingWas()
    {
        var announcements = new EndingAnnouncements();
        announcements.ShouldAnnounce(BandFate.SpearSideEnded);

        Assert.True(announcements.ShouldAnnounce(BandFate.Ended));
        Assert.False(announcements.ShouldAnnounce(BandFate.Ended));
    }

    [Fact]
    public void ABandThatEndsAllAtOnceIsAnnouncedOnce()
    {
        var announcements = new EndingAnnouncements();

        Assert.True(announcements.ShouldAnnounce(BandFate.Ended));
        Assert.False(announcements.ShouldAnnounce(BandFate.Ended));
    }

    // A reopened line says nothing on screen, but its closing again is news again.
    [Fact]
    public void AReopenedLineClosingAgainIsAnnouncedAgain()
    {
        var announcements = new EndingAnnouncements();
        announcements.ShouldAnnounce(BandFate.SpearSideEnded);

        Assert.False(announcements.ShouldAnnounce(BandFate.Living));
        Assert.True(announcements.ShouldAnnounce(BandFate.SpearSideEnded));
    }
}
