using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class AmbientSourceTests
{
    private const int SampleRate = 22050;

    // Oblique on purpose: none of the five densities is 0 or 1, so no formula's arithmetic
    // quietly cancels.
    private static readonly AmbientParameters Busy = new(
        BirdDensity: 0.65f, InsectDensity: 0.4f, CorvidDensity: 0.3f, RustleDensity: 0.55f, Hush: 0.1f);

    [Fact]
    public void ReadFillsTheWholeBufferAndStaysWithinUnitRange()
    {
        var source = new AmbientSource(SampleRate, seed: 42);
        source.Set(Busy);
        var buffer = new float[4096];
        // A sentinel outside the valid range: anything left at it means Read skipped a sample.
        Array.Fill(buffer, 42.0f);

        source.Read(buffer);

        Assert.All(buffer, sample => Assert.NotEqual(42.0f, sample));
        Assert.All(buffer, sample => Assert.InRange(sample, -1.0f, 1.0f));
    }

    [Fact]
    public void SameSeedAndParametersGiveTheSameOutput()
    {
        var a = new AmbientSource(SampleRate, seed: 123);
        a.Set(Busy);
        var b = new AmbientSource(SampleRate, seed: 123);
        b.Set(Busy);

        var bufferA = SampleSourceRenderer.Render(a, seconds: 10.0f, chunkSamples: 1024);
        var bufferB = SampleSourceRenderer.Render(b, seconds: 10.0f, chunkSamples: 1024);

        Assert.Equal(bufferA, bufferB);
    }

    [Fact]
    public void HigherBirdDensityGivesMoreEnergyOverALongRender()
    {
        var quiet = new AmbientSource(SampleRate, seed: 99);
        quiet.Set(new AmbientParameters(BirdDensity: 0.05f, InsectDensity: 0.0f, CorvidDensity: 0.0f, RustleDensity: 0.0f, Hush: 0.0f));
        var busy = new AmbientSource(SampleRate, seed: 99);
        busy.Set(new AmbientParameters(BirdDensity: 1.0f, InsectDensity: 0.0f, CorvidDensity: 0.0f, RustleDensity: 0.0f, Hush: 0.0f));

        var quietSamples = SampleSourceRenderer.Render(quiet, seconds: 30.0f, chunkSamples: 1024);
        var busySamples = SampleSourceRenderer.Render(busy, seconds: 30.0f, chunkSamples: 1024);

        Assert.True(Analysis.Rms(busySamples) > Analysis.Rms(quietSamples));
    }

    // Winter: every event density at zero, only the hush left. It has to be audible - emptiness
    // that is a deliberate quiet bed, not a missing sound - but nowhere near the level a busy
    // summer bed sits at.
    [Fact]
    public void HushAloneProducesAQuietNonSilentBed()
    {
        var source = new AmbientSource(SampleRate, seed: 7);
        source.Set(new AmbientParameters(BirdDensity: 0.0f, InsectDensity: 0.0f, CorvidDensity: 0.0f, RustleDensity: 0.0f, Hush: 1.0f));

        var samples = SampleSourceRenderer.Render(source, seconds: 10.0f, chunkSamples: 1024);
        var rms = Analysis.Rms(samples);

        Assert.True(rms > 0.001f, $"hush was inaudible at rms {rms}");
        Assert.True(rms < 0.05f, $"hush was too loud at rms {rms}");
    }

    // The single most valuable test here: an event that starts near the end of one Read call has
    // to keep playing, unchanged, into the next. Rendering the same seed and parameters through
    // two very different chunk sizes and requiring bit-identical output is what proves the active
    // voice pool carries its cursors across the boundary rather than restarting or dropping them.
    [Fact]
    public void AnEventSpanningABufferBoundaryIsContinuousAcrossChunkSizes()
    {
        var small = new AmbientSource(SampleRate, seed: 2024);
        small.Set(Busy);
        var large = new AmbientSource(SampleRate, seed: 2024);
        large.Set(Busy);

        var smallChunks = SampleSourceRenderer.Render(small, seconds: 30.0f, chunkSamples: 512);
        var largeChunks = SampleSourceRenderer.Render(large, seconds: 30.0f, chunkSamples: 4096);

        Assert.Equal(smallChunks, largeChunks);
    }

    [Fact]
    public void ThePoolNeverGrowsWithoutBound()
    {
        var source = new AmbientSource(SampleRate, seed: 5);
        // Every density pinned to maximum, which is the pathological pile-up case the pool cap
        // exists for: the render must still stay within range rather than blowing up the mix.
        source.Set(new AmbientParameters(BirdDensity: 1.0f, InsectDensity: 1.0f, CorvidDensity: 1.0f, RustleDensity: 1.0f, Hush: 1.0f));

        var samples = SampleSourceRenderer.Render(source, seconds: 30.0f, chunkSamples: 1024);

        Assert.All(samples, sample => Assert.InRange(sample, -1.0f, 1.0f));
    }
}
