using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

public class TickAccumulatorTests
{
    [Fact]
    public void AdvanceReportsNoTickForLessThanOneInterval()
    {
        var accumulator = new TickAccumulator(1.0);

        Assert.False(accumulator.Advance(0.5));
    }

    [Fact]
    public void AdvanceReportsATickForExactlyOneInterval()
    {
        var accumulator = new TickAccumulator(1.0);

        Assert.True(accumulator.Advance(1.0));
    }

    [Fact]
    public void AdvanceReportsAtMostOneTickForALargerDelta()
    {
        var accumulator = new TickAccumulator(1.0);

        Assert.True(accumulator.Advance(2.5));
    }

    [Fact]
    public void AdvancePreservesTheRemainderAcrossCalls()
    {
        var accumulator = new TickAccumulator(1.0);

        Assert.True(accumulator.Advance(1.3));
        // The 0.3 left over from the tick above plus 0.6 is not yet a full interval.
        Assert.False(accumulator.Advance(0.6));
        // The remaining 0.1 finally completes it.
        Assert.True(accumulator.Advance(0.1));
    }

    [Fact]
    public void TickAsSoonAsPossibleMakesTheNextAdvanceTickImmediately()
    {
        var accumulator = new TickAccumulator(1.0);

        accumulator.TickAsSoonAsPossible();

        Assert.True(accumulator.Advance(0.0));
    }
}
