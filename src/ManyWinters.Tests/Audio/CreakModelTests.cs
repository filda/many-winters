using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class CreakModelTests
{
    private const int SampleRate = 22050;
    private const float StandardDurationSeconds = 1.3f;

    private const float WindowSeconds = 0.02f;

    // Oblique on purpose: Size and Strain both sit away from 0 and 1, and the duration is not a
    // round number, so no formula's arithmetic quietly cancels.
    private static readonly Creak Standard = new(Size: 0.35f, Strain: 0.65f);

    [Fact]
    public void SameSeedRendersAnIdenticalBuffer()
    {
        var first = CreakModel.Render(Standard, StandardDurationSeconds, SampleRate, 613);
        var second = CreakModel.Render(Standard, StandardDurationSeconds, SampleRate, 613);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentSeedsRenderDifferentBuffers()
    {
        var first = CreakModel.Render(Standard, StandardDurationSeconds, SampleRate, 613);
        var second = CreakModel.Render(Standard, StandardDurationSeconds, SampleRate, 614);

        Assert.NotEqual(first, second);
    }

    // Asserting the exact target rather than "at most 1" is what keeps a broken normalisation
    // from passing.
    [Theory]
    [InlineData(0.1f, 0.2f)]
    [InlineData(0.9f, 0.95f)]
    [InlineData(0.5f, 0.0f)]
    public void EveryRenderNormalisesToPeak09Exactly(float size, float strain)
    {
        var samples = CreakModel.Render(new Creak(size, strain), StandardDurationSeconds, SampleRate, 42);

        Assert.Equal(0.9f, Analysis.Peak(samples), 4);
    }

    // Centroid, not DominantFrequency. A creak that has a measurable dominant pitch has gone
    // back to being a plucked string, which is exactly what the ear rejected; with an irregular
    // catch train there is no single peak to find and the measurement returns bin zero for
    // every size. Where the weight of the spectrum sits still orders cleanly.
    [Fact]
    public void BiggerSizeSoundsLower()
    {
        var small = CreakModel.Render(new Creak(0.2f, 0.8f), StandardDurationSeconds, SampleRate, 42);
        var large = CreakModel.Render(new Creak(0.9f, 0.8f), StandardDurationSeconds, SampleRate, 42);

        Assert.True(Analysis.SpectralCentroid(small, SampleRate) > Analysis.SpectralCentroid(large, SampleRate));
    }

    // Counting slips, not measuring a frequency. An earlier creak glided one sawtooth smoothly
    // across the whole render, so DominantFrequency of a slice tracked that glide - and the ear
    // called the result bungee jumping, because a smooth sweep is a slide whistle. The model now
    // fires discrete bursts, and what accelerates is how often they come. Frequency of a slice
    // now reports whichever trunk mode the burst happened to excite, which says nothing.
    [Fact]
    public void TheSlipsComeFasterAsTheCutDeepens()
    {
        var samples = CreakModel.Render(Standard, StandardDurationSeconds, SampleRate, 613);
        var third = samples.Length / 3;

        var early = CountSlips(samples.AsSpan(0, third));
        var late = CountSlips(samples.AsSpan(samples.Length - third, third));

        Assert.True(late > early, $"early {early}, late {late}");
    }

    [Fact]
    public void HigherStrainGivesMoreSlips()
    {
        var slow = CreakModel.Render(new Creak(0.35f, 0.25f), StandardDurationSeconds, SampleRate, 613);
        var fast = CreakModel.Render(new Creak(0.35f, 0.9f), StandardDurationSeconds, SampleRate, 613);

        Assert.True(
            CountSlips(fast) > CountSlips(slow),
            $"slow {CountSlips(slow)}, fast {CountSlips(fast)}");
    }

    [Fact]
    public void RenderLastsTheRequestedDuration()
    {
        var samples = CreakModel.Render(Standard, 0.73f, SampleRate, 5);

        Assert.Equal((int)(0.73f * SampleRate), samples.Length);
    }

    // Steady band-limited noise under a fixed envelope has a windowed-RMS coefficient of variation
    // around 0.13 - sampling noise, nothing else. The stutter has to clear that by a wide margin for
    // the catch-and-release to be real rather than an artefact of the resonator bank's own texture.
    [Fact]
    public void TheStutterIsReal()
    {
        var samples = CreakModel.Render(Standard, StandardDurationSeconds, SampleRate, 99);
        var windowRms = WindowRms(samples);

        var mean = windowRms.Average();
        var variance = windowRms.Select(v => (v - mean) * (v - mean)).Average();
        var coefficientOfVariation = MathF.Sqrt(variance) / mean;

        Assert.True(coefficientOfVariation > 0.3f, $"cv was {coefficientOfVariation}");
    }

    // Two thresholds, not one: a single level miscounts on a decaying burst that blips back over
    // it, which is what cost FrictionModelTests a round.
    private static int CountSlips(ReadOnlySpan<float> samples)
    {
        const int window = (int)(0.004f * SampleRate);
        var windowCount = samples.Length / window;
        if (windowCount == 0)
        {
            return 0;
        }

        var levels = new float[windowCount];
        for (var w = 0; w < windowCount; w++)
        {
            levels[w] = Analysis.Rms(samples.Slice(w * window, window));
        }

        var peak = levels.Max();
        var rise = peak * 0.30f;
        var fall = peak * 0.12f;

        var count = 0;
        var above = false;
        foreach (var level in levels)
        {
            if (!above && level > rise)
            {
                count++;
                above = true;
            }
            else if (above && level < fall)
            {
                above = false;
            }
        }

        return count;
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
