using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class WindSourceTests
{
    private const int SampleRate = 22050;

    [Fact]
    public void ReadFillsTheWholeBufferAndStaysWithinUnitRange()
    {
        var source = new WindSource(SampleRate, seed: 42);
        var buffer = new float[4096];
        // A sentinel outside the valid range: anything left at it means Read skipped a sample.
        Array.Fill(buffer, 42.0f);

        source.Read(buffer);

        Assert.All(buffer, sample => Assert.NotEqual(42.0f, sample));
        Assert.All(buffer, sample => Assert.InRange(sample, -1.0f, 1.0f));
    }

    [Fact]
    public void HigherStrengthProducesHigherRms()
    {
        var quiet = new WindSource(SampleRate, seed: 5);
        quiet.Set(new WindParameters(0.2f, 0.3f));
        var loud = new WindSource(SampleRate, seed: 5);
        loud.Set(new WindParameters(0.8f, 0.3f));

        var quietSamples = SampleSourceRenderer.Render(quiet, seconds: 2.0f, chunkSamples: 512);
        var loudSamples = SampleSourceRenderer.Render(loud, seconds: 2.0f, chunkSamples: 512);

        Assert.True(Analysis.Rms(loudSamples) > Analysis.Rms(quietSamples));
    }

    [Fact]
    public void ParameterChangeBetweenReadsDoesNotClick()
    {
        var source = new WindSource(SampleRate, seed: 1701);
        var first = new float[4096];
        source.Read(first);

        source.Set(new WindParameters(0.9f, 0.9f));
        var second = new float[4096];
        source.Read(second);

        var boundaryJump = MathF.Abs(second[0] - first[^1]);
        var largestInternalJump = MathF.Max(MaxAbsoluteJump(first), MaxAbsoluteJump(second));

        Assert.True(
            boundaryJump <= largestInternalJump + 1e-4f,
            $"boundary jump {boundaryJump} exceeded the largest internal jump {largestInternalJump}");
    }

    [Fact]
    public void SameSeedProducesTheSameBuffer()
    {
        var a = new WindSource(SampleRate, seed: 123);
        var b = new WindSource(SampleRate, seed: 123);
        a.Set(new WindParameters(0.6f, 0.4f));
        b.Set(new WindParameters(0.6f, 0.4f));

        var bufferA = SampleSourceRenderer.Render(a, seconds: 1.0f, chunkSamples: 512);
        var bufferB = SampleSourceRenderer.Render(b, seconds: 1.0f, chunkSamples: 512);

        Assert.Equal(bufferA, bufferB);
    }

    [Fact]
    public void HigherGustinessMovesTheEnvelopeFaster()
    {
        var steady = new WindSource(SampleRate, seed: 99);
        steady.Set(new WindParameters(0.7f, 0.02f));
        var gusty = new WindSource(SampleRate, seed: 99);
        gusty.Set(new WindParameters(0.7f, 1.0f));

        var steadySamples = SampleSourceRenderer.Render(steady, seconds: 20.0f, chunkSamples: 1024);
        var gustySamples = SampleSourceRenderer.Render(gusty, seconds: 20.0f, chunkSamples: 1024);

        var steadyRoughness = EnvelopeRoughness(steadySamples, chunkSize: 11025);
        var gustyRoughness = EnvelopeRoughness(gustySamples, chunkSize: 11025);

        Assert.True(
            gustyRoughness > steadyRoughness,
            $"steady roughness {steadyRoughness}, gusty roughness {gustyRoughness}");
    }

    // Loudness alone made every strength sound like the same gale at a different volume. A
    // breeze has to be darker than a storm, not just quieter.
    [Fact]
    public void HigherStrengthIsBrighterAndNotOnlyLouder()
    {
        var breeze = new WindSource(SampleRate, seed: 2024);
        breeze.Set(new WindParameters(0.15f, 0.2f));
        var gale = new WindSource(SampleRate, seed: 2024);
        gale.Set(new WindParameters(0.9f, 0.2f));

        var breezeSamples = SampleSourceRenderer.Render(breeze, seconds: 6.0f, chunkSamples: 1024);
        var galeSamples = SampleSourceRenderer.Render(gale, seconds: 6.0f, chunkSamples: 1024);

        Assert.True(
            Analysis.SpectralCentroid(galeSamples, SampleRate) > Analysis.SpectralCentroid(breezeSamples, SampleRate) * 1.5f,
            "a gale must be audibly brighter than a breeze, not merely louder");
    }

    [Fact]
    public void WhistleIsAbsentWhenGustinessIsZero()
    {
        // Strength 0 pins both filters' centre frequency (300 Hz main, 600 Hz whistle) regardless
        // of the LFO, isolating the whistle mix from the centre frequency also drifting.
        var calm = new WindSource(SampleRate, seed: 7);
        calm.Set(new WindParameters(0.0f, 0.0f));
        var gusty = new WindSource(SampleRate, seed: 7);
        gusty.Set(new WindParameters(0.0f, 1.0f));

        var calmSamples = SampleSourceRenderer.Render(calm, seconds: 5.0f, chunkSamples: 1024);
        var gustySamples = SampleSourceRenderer.Render(gusty, seconds: 5.0f, chunkSamples: 1024);

        // Gustiness 0 zeroes the whistle mix identically, whatever the gain LFO does; gustiness 1
        // lets it ride the top half of a gust, adding energy an octave above the fixed main band.
        var calmCentroid = Analysis.SpectralCentroid(calmSamples, SampleRate);
        var gustyCentroid = Analysis.SpectralCentroid(gustySamples, SampleRate);

        Assert.True(gustyCentroid > calmCentroid, $"calm centroid {calmCentroid}, gusty centroid {gustyCentroid}");
    }

    private static float MaxAbsoluteJump(float[] samples)
    {
        var max = 0.0f;
        for (var i = 1; i < samples.Length; i++)
        {
            max = MathF.Max(max, MathF.Abs(samples[i] - samples[i - 1]));
        }

        return max;
    }

    // How fast the gain envelope moves relative to its own scale: consecutive-chunk RMS
    // differences over the chunk RMS's own variance. Chunks are large enough (0.5 s) that the
    // noise carrier's own estimator jitter is a small fraction of a real gain swing, so this
    // tracks the LFO's rate rather than the noise underneath it.
    private static float EnvelopeRoughness(float[] samples, int chunkSize)
    {
        var chunkCount = samples.Length / chunkSize;
        var envelope = new float[chunkCount];
        for (var c = 0; c < chunkCount; c++)
        {
            envelope[c] = Analysis.Rms(samples.AsSpan(c * chunkSize, chunkSize));
        }

        var mean = envelope.Average();
        double variance = 0.0;
        double squaredDifferences = 0.0;
        for (var i = 0; i < envelope.Length; i++)
        {
            variance += (envelope[i] - mean) * (envelope[i] - mean);
            if (i > 0)
            {
                squaredDifferences += (envelope[i] - envelope[i - 1]) * (envelope[i] - envelope[i - 1]);
            }
        }

        return variance > 0.0 ? (float)(squaredDifferences / variance) : 0.0f;
    }
}
