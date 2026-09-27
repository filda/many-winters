using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class WhiteNoiseTests
{
    [Fact]
    public void MeanIsNearZeroAndRmsMatchesUniformNoise()
    {
        var noise = new WhiteNoise(new Rng(8675309));
        var samples = new float[100_000];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = noise.Next();
        }

        var mean = samples.Average();

        // RMS of a uniform distribution over [-1, 1) is 1/sqrt(3) ~= 0.577.
        Assert.InRange(mean, -0.02f, 0.02f);
        Assert.InRange(Analysis.Rms(samples), 0.57f, 0.585f);
    }

    [Fact]
    public void StaysInsideTheUnitInterval()
    {
        var noise = new WhiteNoise(new Rng(271828));
        var samples = new float[10_000];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = noise.Next();
        }

        Assert.All(samples, sample => Assert.InRange(sample, -1.0f, 0.99999994f));
    }
}
