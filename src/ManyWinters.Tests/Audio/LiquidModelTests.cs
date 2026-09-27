using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class LiquidModelTests
{
    private const int SampleRate = 22050;

    private static readonly LiquidSplash Step = new(Depth: 0.3f, Vigour: 0.7f);

    [Fact]
    public void SameSeedRendersAnIdenticalBuffer()
    {
        Assert.Equal(
            LiquidModel.Render(Step, SampleRate, 4231),
            LiquidModel.Render(Step, SampleRate, 4231));
    }

    [Fact]
    public void DifferentSeedsRenderDifferentBuffers()
    {
        Assert.NotEqual(
            LiquidModel.Render(Step, SampleRate, 4231),
            LiquidModel.Render(Step, SampleRate, 4232));
    }

    [Theory]
    [InlineData(0.0f, 0.2f)]
    [InlineData(0.3f, 0.7f)]
    [InlineData(1.0f, 1.0f)]
    public void EveryRenderNormalisesToTheSamePeak(float depth, float vigour)
    {
        var samples = LiquidModel.Render(new LiquidSplash(depth, vigour), SampleRate, 77);

        Assert.Equal(0.9f, Analysis.Peak(samples), 4);
    }

    // What this model has that a grain cloud does not: three bands ending at three different
    // times. The spray has to outlast the slap and die before the water underneath - get that
    // ordering wrong and it is a hit on something wet rather than a splash.
    [Fact]
    public void TheSprayOutlivesTheSlapAndDiesBeforeTheWater()
    {
        var samples = LiquidModel.Render(new LiquidSplash(Depth: 0.8f, Vigour: 0.9f), SampleRate, 913);
        var third = samples.Length / 3;

        var slapEarly = BandEnergy(samples.AsSpan(0, third), 600.0f, 2000.0f);
        var slapLate = BandEnergy(samples.AsSpan(third, third), 600.0f, 2000.0f);
        var sprayLate = BandEnergy(samples.AsSpan(third, third), 2200.0f, 4200.0f);
        var sprayLast = BandEnergy(samples.AsSpan(2 * third, third), 2200.0f, 4200.0f);
        var waterLast = BandEnergy(samples.AsSpan(2 * third, third), 0.0f, 400.0f);

        // The slap is gone by the middle third, where the spray is still going.
        Assert.True(slapLate < slapEarly * 0.2, $"slap early {slapEarly}, late {slapLate}");

        // And by the last third the spray has gone while the water has not.
        Assert.True(sprayLast < sprayLate * 0.5, $"spray middle {sprayLate}, last {sprayLast}");
        Assert.True(waterLast > sprayLast, $"water {waterLast}, spray {sprayLast}");
    }

    [Fact]
    public void DeeperWaterIsDarker()
    {
        var shallow = LiquidModel.Render(Step with { Depth = 0.05f }, SampleRate, 55);
        var deep = LiquidModel.Render(Step with { Depth = 1.0f }, SampleRate, 55);

        Assert.True(
            Analysis.SpectralCentroid(deep, SampleRate) < Analysis.SpectralCentroid(shallow, SampleRate));
    }

    [Fact]
    public void DeeperWaterLastsLonger()
    {
        var shallow = LiquidModel.Render(Step with { Depth = 0.0f }, SampleRate, 55);
        var deep = LiquidModel.Render(Step with { Depth = 1.0f }, SampleRate, 55);

        Assert.True(deep.Length > shallow.Length);
    }

    // Vigour brings in the spray, which is the highest band there is, so a harder splash is a
    // brighter one. It is also the only thing separating a stamp from a step.
    [Fact]
    public void AHarderSplashIsBrighter()
    {
        var gentle = LiquidModel.Render(Step with { Vigour = 0.05f }, SampleRate, 606);
        var hard = LiquidModel.Render(Step with { Vigour = 1.0f }, SampleRate, 606);

        Assert.True(
            Analysis.SpectralCentroid(hard, SampleRate) > Analysis.SpectralCentroid(gentle, SampleRate));
    }

    private static double BandEnergy(ReadOnlySpan<float> samples, float lower, float upper)
    {
        var spectrum = Analysis.Fft(samples);

        double energy = 0.0;
        for (var bin = 0; bin < spectrum.Length; bin++)
        {
            var frequency = Analysis.BinFrequency(bin, spectrum.Length, SampleRate);
            if (frequency >= lower && frequency < upper)
            {
                energy += (double)spectrum[bin] * spectrum[bin];
            }
        }

        return energy;
    }
}
