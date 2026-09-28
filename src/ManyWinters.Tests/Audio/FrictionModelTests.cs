using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class FrictionModelTests
{
    private const int SampleRate = 22050;
    private const int StrokeCount = 4;

    private const float WindowSeconds = 0.02f;

    private const float LowHighSplitFrequency = 500.0f;
    private const float HighBandFloorFrequency = 1500.0f;

    // Oblique on purpose: neither StrokesPerSecond nor strokeCount is 1, and Grit/Pressure sit
    // away from 0 and 1 so no formula's arithmetic quietly cancels.
    private static readonly FrictionStroke Stroke = new(Grit: 0.35f, Pressure: 0.6f, StrokesPerSecond: 1.7f);

    [Fact]
    public void SameSeedRendersAnIdenticalBuffer()
    {
        var first = FrictionModel.Render(Stroke, StrokeCount, SampleRate, 613);
        var second = FrictionModel.Render(Stroke, StrokeCount, SampleRate, 613);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentSeedsRenderDifferentBuffers()
    {
        var first = FrictionModel.Render(Stroke, StrokeCount, SampleRate, 613);
        var second = FrictionModel.Render(Stroke, StrokeCount, SampleRate, 614);

        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData(0.2f, 0.7f, 1.8f)]
    [InlineData(0.8f, 0.55f, 2.4f)]
    [InlineData(0.35f, 1.0f, 1.3f)]
    public void EveryRenderNormalisesToPeak09Exactly(float grit, float pressure, float strokesPerSecond)
    {
        var samples = FrictionModel.Render(new FrictionStroke(grit, pressure, strokesPerSecond), 5, SampleRate, 42);

        Assert.Equal(0.9f, Analysis.Peak(samples), 4);
    }

    // The swell is the whole gesture, so it has to be countable: slice the buffer into short
    // windows, take each window's RMS, and count the runs that climb above a fraction of the
    // loudest window. A percussive re-trigger or a continuous drone would both fail this either by
    // never reaching a low-below-high transition or by merging every stroke into one run.
    [Fact]
    public void RenderContainsExactlyStrokeCountSwells()
    {
        var samples = FrictionModel.Render(Stroke, StrokeCount, SampleRate, 7);

        Assert.Equal(StrokeCount, CountSwells(samples));
    }

    [Fact]
    public void TheBufferFallsCloseToSilenceBetweenStrokes()
    {
        var samples = FrictionModel.Render(Stroke, StrokeCount, SampleRate, 7);
        var windowRms = WindowRms(samples);

        var peakWindow = windowRms.Max();
        Assert.Contains(windowRms, rms => rms < peakWindow * 0.1f);
    }

    [Fact]
    public void FinerGritGivesAHigherSpectralCentroid()
    {
        var coarse = FrictionModel.Render(Stroke with { Grit = 0.15f }, StrokeCount, SampleRate, 11);
        var fine = FrictionModel.Render(Stroke with { Grit = 0.85f }, StrokeCount, SampleRate, 11);

        Assert.True(
            Analysis.SpectralCentroid(fine, SampleRate) > Analysis.SpectralCentroid(coarse, SampleRate));
    }

    // Heavier pressure adds a low-passed layer under the hiss, so the energy below 500 Hz should
    // grow relative to the energy above 1500 Hz as Pressure rises, even though the bandpassed
    // layers above that split are untouched by Pressure.
    [Fact]
    public void HigherPressureShiftsEnergyDownwards()
    {
        var light = FrictionModel.Render(Stroke with { Pressure = 0.2f }, StrokeCount, SampleRate, 5);
        var heavy = FrictionModel.Render(Stroke with { Pressure = 0.95f }, StrokeCount, SampleRate, 5);

        Assert.True(LowToHighEnergyRatio(heavy) > LowToHighEnergyRatio(light));
    }

    [Fact]
    public void RenderedLengthFollowsStrokeCountOverStrokesPerSecond()
    {
        var samples = FrictionModel.Render(Stroke, StrokeCount, SampleRate, 9);

        var nominalSeconds = StrokeCount / Stroke.StrokesPerSecond;
        var actualSeconds = samples.Length / (float)SampleRate;

        // At least the nominal length, and no more than the nominal length plus a generous tail
        // allowance - the render adds a short tail so the last stroke's swell is never clipped.
        Assert.InRange(actualSeconds, nominalSeconds, nominalSeconds + 0.5f);
    }

    [Fact]
    public void NoTwoStrokesAreIdentical()
    {
        var samples = FrictionModel.Render(Stroke, StrokeCount, SampleRate, 3);
        var slotSamples = (int)(SampleRate / Stroke.StrokesPerSecond);

        var firstStroke = samples.AsSpan(0, slotSamples);
        var secondStroke = samples.AsSpan(slotSamples, slotSamples);

        Assert.NotEqual(firstStroke.ToArray(), secondStroke.ToArray());
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

    private static int CountSwells(float[] samples)
    {
        var windowRms = WindowRms(samples);
        var peak = windowRms.Max();

        // Two thresholds, not one. Band-limited noise makes a single window in a decaying tail
        // blip back over a lone threshold and register as another stroke; the gap between the
        // two levels is what a real stroke boundary has to cross and a blip does not.
        var rise = peak * 0.35f;
        var fall = peak * 0.15f;

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

    private static float LowToHighEnergyRatio(float[] samples)
    {
        var spectrum = Analysis.Fft(samples);

        double low = 0.0;
        double high = 0.0;
        for (var bin = 0; bin < spectrum.Length; bin++)
        {
            var frequency = Analysis.BinFrequency(bin, spectrum.Length, SampleRate);
            if (frequency < LowHighSplitFrequency)
            {
                low += spectrum[bin];
            }
            else if (frequency > HighBandFloorFrequency)
            {
                high += spectrum[bin];
            }
        }

        return (float)(low / high);
    }
}
