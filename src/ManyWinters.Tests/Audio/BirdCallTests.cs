using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class BirdCallTests
{
    private const int SampleRate = 22050;

    private const float WindowSeconds = 0.005f;

    // Oblique on purpose: PitchHz and Brightness both sit away from round numbers, so no
    // formula's arithmetic quietly cancels.
    private static readonly BirdVoice Voice = new(PitchHz: 2350.0f, Brightness: 0.65f);

    [Fact]
    public void SameSeedRendersAnIdenticalBuffer()
    {
        var first = BirdCall.Render(Voice, SampleRate, 613);
        var second = BirdCall.Render(Voice, SampleRate, 613);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentSeedsRenderDifferentBuffers()
    {
        var first = BirdCall.Render(Voice, SampleRate, 613);
        var second = BirdCall.Render(Voice, SampleRate, 614);

        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData(1400.0f, 0.2f)]
    [InlineData(3200.0f, 0.95f)]
    public void EveryRenderNormalisesToPeak09Exactly(float pitchHz, float brightness)
    {
        var samples = BirdCall.Render(new BirdVoice(pitchHz, brightness), SampleRate, 42);

        Assert.Equal(0.9f, Analysis.Peak(samples), 4);
    }

    [Fact]
    public void AHigherPitchGivesAHigherSpectralCentroid()
    {
        var low = BirdCall.Render(Voice with { PitchHz = 1600.0f }, SampleRate, 7);
        var high = BirdCall.Render(Voice with { PitchHz = 3400.0f }, SampleRate, 7);

        Assert.True(Analysis.SpectralCentroid(high, SampleRate) > Analysis.SpectralCentroid(low, SampleRate));
    }

    // A phrase, not a note: counting the runs a windowed RMS climbs above a fraction of the
    // loudest window, the same technique FrictionModelTests and CreakModelTests use for their own
    // repeated events. A single sustained tone would register as one run whatever its length.
    [Fact]
    public void RenderContainsMoreThanOneNote()
    {
        var samples = BirdCall.Render(Voice, SampleRate, 21);

        Assert.True(CountBursts(samples) > 1);
    }

    // The chirp is the glide: a note's own pitch at its start has to differ measurably from its
    // pitch at its end. Located via the same hysteresis burst-finder rather than assuming a fixed
    // note length, since note duration is itself drawn from Rng.
    [Fact]
    public void NotesGlideFromStartToEnd()
    {
        var samples = BirdCall.Render(Voice, SampleRate, 21);
        var (start, end) = FirstBurst(samples);

        var windowSamples = Math.Min((int)(0.01f * SampleRate), (end - start) / 3);
        Assert.True(windowSamples > 8, "the first note was too short to sample twice");

        var early = Analysis.DominantFrequency(samples.AsSpan(start, windowSamples), SampleRate);
        var late = Analysis.DominantFrequency(samples.AsSpan(end - windowSamples, windowSamples), SampleRate);

        Assert.NotEqual(early, late);
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

    // Two thresholds, not one: a single level miscounts on a decaying note's tail blipping back
    // over it, which is what cost FrictionModelTests a round.
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

    private static (int Start, int End) FirstBurst(float[] samples)
    {
        var windowSamples = (int)(WindowSeconds * SampleRate);
        var windowRms = WindowRms(samples);
        var peak = windowRms.Max();
        var rise = peak * 0.3f;
        var fall = peak * 0.12f;

        var startWindow = -1;
        for (var w = 0; w < windowRms.Length; w++)
        {
            if (windowRms[w] > rise)
            {
                startWindow = w;
                break;
            }
        }

        Assert.True(startWindow >= 0, "no note was found above the rise threshold");

        var endWindow = windowRms.Length - 1;
        for (var w = startWindow + 1; w < windowRms.Length; w++)
        {
            if (windowRms[w] < fall)
            {
                endWindow = w;
                break;
            }
        }

        return (startWindow * windowSamples, Math.Max((endWindow * windowSamples) + windowSamples, (startWindow * windowSamples) + windowSamples));
    }
}
