using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class GranularModelTests
{
    private const int SampleRate = 22050;

    private static readonly GranularSurface Stone = new(
        GrainsPerSecond: 260.0f, GrainHardness: 0.9f, ResonanceHz: 1700.0f, ResonanceDamping: 0.3f, NoiseWash: 0.0f);

    private static readonly GranularSurface Snow = new(
        GrainsPerSecond: 2400.0f, GrainHardness: 0.35f, ResonanceHz: 550.0f, ResonanceDamping: 0.95f, NoiseWash: 0.25f);

    private static readonly GranularGesture Footstep = new(AttackSeconds: 0.006f, T60Seconds: 0.09f, Intensity: 0.8f);

    [Fact]
    public void SameSeedRendersAnIdenticalBuffer()
    {
        var first = GranularModel.Render(Stone, Footstep, SampleRate, 613);
        var second = GranularModel.Render(Stone, Footstep, SampleRate, 613);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentSeedsRenderDifferentBuffers()
    {
        var first = GranularModel.Render(Stone, Footstep, SampleRate, 613);
        var second = GranularModel.Render(Stone, Footstep, SampleRate, 614);

        Assert.NotEqual(first, second);
    }

    // Asserting the exact target rather than "at most 1" is what keeps a broken normalisation
    // from passing.
    [Theory]
    [InlineData(0.9f)]
    [InlineData(0.35f)]
    public void EveryRenderNormalisesToPeakExactly(float grainHardness)
    {
        var surface = Stone with { GrainHardness = grainHardness };
        var samples = GranularModel.Render(surface, Footstep, SampleRate, 42);

        Assert.Equal(0.9f, Analysis.Peak(samples), 4);
    }

    [Fact]
    public void AHardSurfaceHasAHigherSpectralCentroidThanASoftOne()
    {
        var stone = GranularModel.Render(Stone, Footstep, SampleRate, 11);
        var snow = GranularModel.Render(Snow, Footstep, SampleRate, 11);

        Assert.True(Analysis.SpectralCentroid(stone, SampleRate) > Analysis.SpectralCentroid(snow, SampleRate));
    }

    // The property that makes rustle and footsteps one model rather than two: damping turns a
    // ringing body into a dry scatter. Two measurements, because either alone can be satisfied
    // by an accident - the body has to be both audible at its own frequency and filling the
    // gaps between grains.
    //
    // DurationAbove is the wrong instrument here and was tried first: it finds the last sample
    // over a threshold, which at any usable grain rate is set by where the final grain happened
    // to land, not by what happened after it. It read 0.1839 against 0.1840 across the whole
    // range of the knob.
    [Fact]
    public void ALiveBodyRingsWhereADryScatterOnlyClicks()
    {
        var surface = new GranularSurface(
            GrainsPerSecond: 40.0f, GrainHardness: 0.6f, ResonanceHz: 600.0f, ResonanceDamping: 0.2f, NoiseWash: 0.0f);
        var gesture = new GranularGesture(AttackSeconds: 0.01f, T60Seconds: 0.4f, Intensity: 0.6f);

        var live = GranularModel.Render(surface, gesture, SampleRate, 305);
        var dry = GranularModel.Render(surface with { ResonanceDamping = 1.0f }, gesture, SampleRate, 305);

        // Energy sits at the body's own frequency when it is left to ring.
        Assert.True(
            ResonanceShare(live) > ResonanceShare(dry) * 2.0f,
            $"live {ResonanceShare(live)}, dry {ResonanceShare(dry)}");

        // And the ring fills the silence between grains, so the signal is less spiky.
        Assert.True(
            CrestFactor(live) < CrestFactor(dry),
            $"live {CrestFactor(live)}, dry {CrestFactor(dry)}");
    }

    private static float CrestFactor(float[] samples) => Analysis.Peak(samples) / Analysis.Rms(samples);

    // Share of the spectrum's energy sitting around the body's 600 Hz fundamental.
    private static double ResonanceShare(float[] samples)
    {
        var spectrum = Analysis.Fft(samples);
        double inBand = 0.0;
        double total = 0.0;
        for (var bin = 0; bin < spectrum.Length; bin++)
        {
            var energy = (double)spectrum[bin] * spectrum[bin];
            total += energy;
            var frequency = Analysis.BinFrequency(bin, spectrum.Length, SampleRate);
            if (frequency is >= 500.0f and < 800.0f)
            {
                inBand += energy;
            }
        }

        return total > 0.0 ? inBand / total : 0.0;
    }

    // Everything else equal, more grains per second means a denser signal, so the crest factor
    // (peak, fixed by normalisation, over RMS) falls as the rate rises.
    [Fact]
    public void AHigherGrainRateLowersTheCrestFactor()
    {
        var sparse = Snow with { GrainsPerSecond = 300.0f, NoiseWash = 0.0f };
        var dense = Snow with { GrainsPerSecond = 3000.0f, NoiseWash = 0.0f };

        var sparseSamples = GranularModel.Render(sparse, Footstep, SampleRate, 8);
        var denseSamples = GranularModel.Render(dense, Footstep, SampleRate, 8);

        var sparseCrest = Analysis.Peak(sparseSamples) / Analysis.Rms(sparseSamples);
        var denseCrest = Analysis.Peak(denseSamples) / Analysis.Rms(denseSamples);

        Assert.True(denseCrest < sparseCrest);
    }

    [Fact]
    public void NoiseWashRaisesRmsOverAQuietStream()
    {
        var quiet = new GranularSurface(
            GrainsPerSecond: 25.0f, GrainHardness: 0.4f, ResonanceHz: 500.0f, ResonanceDamping: 0.9f, NoiseWash: 0.0f);

        var dry = GranularModel.Render(quiet, Footstep, SampleRate, 91);
        var washed = GranularModel.Render(quiet with { NoiseWash = 0.9f }, Footstep, SampleRate, 91);

        Assert.True(Analysis.Rms(washed) > Analysis.Rms(dry));
    }

    // Intensity's effect on RMS is undone by peak normalisation, so instead assert it changes the
    // rendered result at all - and separately, that length tracks the gesture rather than being
    // a fixed constant.
    [Fact]
    public void IntensityChangesTheRenderedResult()
    {
        var soft = GranularModel.Render(Stone, Footstep with { Intensity = 0.3f }, SampleRate, 17);
        var strong = GranularModel.Render(Stone, Footstep with { Intensity = 1.0f }, SampleRate, 17);

        Assert.NotEqual(soft, strong);
    }

    [Fact]
    public void RenderedLengthFollowsAttackAndT60RatherThanBeingFixed()
    {
        var brief = GranularModel.Render(Stone, new GranularGesture(0.006f, 0.05f, 0.8f), SampleRate, 4);
        var long1 = GranularModel.Render(Stone, new GranularGesture(0.08f, 0.55f, 0.5f), SampleRate, 4);

        Assert.True(long1.Length > brief.Length);
    }
}
