using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class PoissonImpulsesTests
{
    [Fact]
    public void CountOverTenSecondsIsNearRateTimesTen()
    {
        const int sampleRate = 22050;
        const float rate = 7.0f;

        var impulses = new PoissonImpulses(new Rng(2024), sampleRate, rate, 0.3f, 0.9f);
        var buffer = new float[sampleRate * 10];
        impulses.Fill(buffer);

        var count = buffer.Count(sample => sample != 0.0f);
        var expected = rate * 10;

        Assert.InRange(count, expected * 0.8, expected * 1.2);
    }

    [Fact]
    public void NonZeroSamplesFallInsideTheConfiguredAmplitudeRangeAndRestAreExactlyZero()
    {
        const int sampleRate = 22050;

        var impulses = new PoissonImpulses(new Rng(55), sampleRate, 15.0f, 0.25f, 0.6f);
        var buffer = new float[sampleRate];
        impulses.Fill(buffer);

        foreach (var sample in buffer)
        {
            if (sample == 0.0f)
            {
                continue;
            }

            Assert.InRange(MathF.Abs(sample), 0.25f, 0.6f);
        }

        Assert.Contains(buffer, sample => sample != 0.0f);
    }
}
