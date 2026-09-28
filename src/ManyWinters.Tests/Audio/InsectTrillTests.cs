using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class InsectTrillTests
{
    private const int SampleRate = 22050;

    // Oblique on purpose: neither value is round, so no formula's arithmetic quietly cancels.
    private const float PitchHz = 5300.0f;
    private const float DurationSeconds = 0.85f;

    private const float WindowSeconds = 0.004f;

    [Fact]
    public void SameSeedRendersAnIdenticalBuffer()
    {
        var first = InsectTrill.Render(PitchHz, DurationSeconds, SampleRate, 613);
        var second = InsectTrill.Render(PitchHz, DurationSeconds, SampleRate, 613);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentSeedsRenderDifferentBuffers()
    {
        var first = InsectTrill.Render(PitchHz, DurationSeconds, SampleRate, 613);
        var second = InsectTrill.Render(PitchHz, DurationSeconds, SampleRate, 614);

        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData(4200.0f, 0.45f)]
    [InlineData(6300.0f, 1.4f)]
    public void EveryRenderNormalisesToPeak09Exactly(float pitchHz, float seconds)
    {
        var samples = InsectTrill.Render(pitchHz, seconds, SampleRate, 42);

        Assert.Equal(0.9f, Analysis.Peak(samples), 4);
    }

    [Fact]
    public void RenderLastsTheRequestedDuration()
    {
        var samples = InsectTrill.Render(PitchHz, DurationSeconds, SampleRate, 5);

        Assert.Equal((int)(DurationSeconds * SampleRate), samples.Length);
    }

    // A rasp, not a wobble: windowed RMS has to swing hard within the trill rather than settling
    // near a constant level, which is what a smooth tremolo or a plain carrier would give.
    [Fact]
    public void TheAmplitudeModulationIsReal()
    {
        var samples = InsectTrill.Render(PitchHz, DurationSeconds, SampleRate, 9);
        var windowRms = WindowRms(samples);

        var mean = windowRms.Average();
        var variance = windowRms.Select(v => (v - mean) * (v - mean)).Average();
        var coefficientOfVariation = MathF.Sqrt(variance) / mean;

        Assert.True(coefficientOfVariation > 0.3f, $"cv was {coefficientOfVariation}");
    }

    [Fact]
    public void AHigherPitchIsBrighter()
    {
        var low = InsectTrill.Render(4200.0f, DurationSeconds, SampleRate, 3);
        var high = InsectTrill.Render(6300.0f, DurationSeconds, SampleRate, 3);

        Assert.True(Analysis.SpectralCentroid(high, SampleRate) > Analysis.SpectralCentroid(low, SampleRate));
    }

    private static float[] WindowRms(float[] samples)
    {
        var windowSamples = (int)(WindowSeconds * SampleRate);
        var windowCount = samples.Length / windowSamples;

        var result = new float[windowCount];
        for (var w = 0; w < windowCount; w++)
        {
            result[w] = Analysis.Rms(samples.AsSpan(w * windowSamples, windowSamples));
        }

        return result;
    }
}
