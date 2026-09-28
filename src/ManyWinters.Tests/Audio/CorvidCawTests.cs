using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class CorvidCawTests
{
    private const int SampleRate = 22050;

    // Oblique on purpose: not 0 or 1, so no formula's arithmetic quietly cancels.
    private const float Size = 0.4f;

    private const float WindowSeconds = 0.02f;

    [Fact]
    public void SameSeedRendersAnIdenticalBuffer()
    {
        var first = CorvidCaw.Render(Size, SampleRate, 613);
        var second = CorvidCaw.Render(Size, SampleRate, 613);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentSeedsRenderDifferentBuffers()
    {
        var first = CorvidCaw.Render(Size, SampleRate, 613);
        var second = CorvidCaw.Render(Size, SampleRate, 614);

        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.9f)]
    public void EveryRenderNormalisesToPeak09Exactly(float size)
    {
        var samples = CorvidCaw.Render(size, SampleRate, 42);

        Assert.Equal(0.9f, Analysis.Peak(samples), 4);
    }

    [Fact]
    public void BiggerSizeSoundsLower()
    {
        var small = CorvidCaw.Render(0.15f, SampleRate, 613);
        var large = CorvidCaw.Render(0.95f, SampleRate, 613);

        Assert.True(Analysis.SpectralCentroid(small, SampleRate) > Analysis.SpectralCentroid(large, SampleRate));
    }

    // Counting caws, not measuring a frequency - the same windowed-RMS hysteresis technique
    // CreakModelTests and FrictionModelTests use for their own repeated events.
    [Fact]
    public void TheCawRepeats()
    {
        var samples = CorvidCaw.Render(Size, SampleRate, 3);

        Assert.True(CountBursts(samples) > 1);
    }

    // A rasp, not a note: the peak FFT bin must not stand far above the spectrum's own mean, the
    // same measurement that separates a caw from a plucked string. A sine at the caw's own
    // fundamental is the plucked-sounding reference the ratio is judged against.
    [Fact]
    public void TheCawIsNotTonal()
    {
        var caw = CorvidCaw.Render(Size, SampleRate, 613);
        var pluckedReference = PluckedReference(220.0f);

        var cawRatio = PeakToMeanRatio(caw);
        var referenceRatio = PeakToMeanRatio(pluckedReference);

        Assert.True(
            cawRatio < referenceRatio / 4.0f,
            $"caw ratio {cawRatio} was not far enough below the plucked reference's {referenceRatio}");
    }

    private static float[] PluckedReference(float frequencyHz)
    {
        const int length = 8192;
        var oscillator = new Oscillator(SampleRate, Waveform.Sine, frequencyHz);
        var envelope = new Envelope(SampleRate, 0.002f, 0.6f);

        var samples = new float[length];
        for (var i = 0; i < length; i++)
        {
            samples[i] = oscillator.Next() * envelope.Next();
        }

        return samples;
    }

    private static float PeakToMeanRatio(float[] samples)
    {
        var spectrum = Analysis.Fft(samples);
        var peak = spectrum.Max();
        var mean = spectrum.Average();

        return peak / mean;
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

    private static int CountBursts(float[] samples)
    {
        var windowRms = WindowRms(samples);
        var peak = windowRms.Max();
        var rise = peak * 0.3f;
        var fall = peak * 0.12f;

        var count = 0;
        var above = false;
        foreach (var rms in windowRms)
        {
            if (!above && rms > rise)
            {
                count++;
                above = true;
            }
            else if (above && rms < fall)
            {
                above = false;
            }
        }

        return count;
    }
}
