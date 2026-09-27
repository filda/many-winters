using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class RandomWalkLfoTests
{
    private const int SampleRate = 22050;

    [Fact]
    public void StaysWithinBoundsAndStepsNeverExceedTheRateBound()
    {
        const float rateHz = 0.3f;
        const float minimum = -2.0f;
        const float maximum = 5.0f;

        var lfo = new RandomWalkLfo(new Rng(777), SampleRate, rateHz, minimum, maximum);
        var maximumStep = (maximum - minimum) * rateHz / SampleRate;

        var previous = lfo.Next();
        Assert.InRange(previous, minimum, maximum);

        for (var i = 0; i < SampleRate * 3; i++)
        {
            var current = lfo.Next();
            Assert.InRange(current, minimum, maximum);
            // A small tolerance covers the boundary clamp, which can shorten but never lengthen a step.
            Assert.True(MathF.Abs(current - previous) <= maximumStep + 1e-6f);
            previous = current;
        }
    }

    [Fact]
    public void HigherRateProducesLargerMeanAbsoluteStep()
    {
        const float minimum = 100.0f;
        const float maximum = 800.0f;
        const int samples = 5000;

        var slow = new RandomWalkLfo(new Rng(31), SampleRate, 0.1f, minimum, maximum);
        var fast = new RandomWalkLfo(new Rng(31), SampleRate, 5.0f, minimum, maximum);

        Assert.True(MeanAbsoluteStep(slow, samples) < MeanAbsoluteStep(fast, samples));
    }

    // The bound and the range check together are satisfied by an LFO that never moves, which is
    // exactly what a per-sample-redrawn target averages out to. This is the assertion that says
    // the modulation is real.
    [Fact]
    public void TraversesMostOfItsRangeWithinAFewPeriods()
    {
        const float rateHz = 0.4f;
        const float minimum = 300.0f;
        const float maximum = 1200.0f;

        var lfo = new RandomWalkLfo(new Rng(1234), SampleRate, rateHz, minimum, maximum);
        var lowest = float.MaxValue;
        var highest = float.MinValue;
        for (var i = 0; i < (int)(SampleRate * 10 / rateHz); i++)
        {
            var value = lfo.Next();
            lowest = MathF.Min(lowest, value);
            highest = MathF.Max(highest, value);
        }

        Assert.True(highest - lowest > 0.6f * (maximum - minimum), $"swing was only {highest - lowest}");
    }

    private static float MeanAbsoluteStep(RandomWalkLfo lfo, int samples)
    {
        var previous = lfo.Next();
        double sum = 0.0;
        for (var i = 0; i < samples; i++)
        {
            var current = lfo.Next();
            sum += MathF.Abs(current - previous);
            previous = current;
        }

        return (float)(sum / samples);
    }
}
