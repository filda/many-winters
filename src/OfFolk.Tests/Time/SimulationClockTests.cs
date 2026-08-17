using OfFolk.Core.Time;

namespace OfFolk.Tests.Time;

public class SimulationClockTests
{
    [Fact]
    public void StartsAtTickZero()
    {
        var clock = new SimulationClock();

        Assert.Equal(0, clock.CurrentTick);
    }

    [Fact]
    public void AdvanceIncreasesCurrentTick()
    {
        var clock = new SimulationClock();

        clock.Advance(5);
        clock.Advance(3);

        Assert.Equal(8, clock.CurrentTick);
    }

    [Fact]
    public void AdvanceDefaultsToOneTick()
    {
        var clock = new SimulationClock();

        clock.Advance();

        Assert.Equal(1, clock.CurrentTick);
    }

    [Fact]
    public void AdvanceRejectsNegativeTicks()
    {
        var clock = new SimulationClock();

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(-1));
    }
}
