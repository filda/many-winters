using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class PinkNoiseTests
{
    private const int SampleRate = 22050;
    private const int SampleCount = 1 << 17;

    [Fact]
    public void SpectralCentroidIsLowerThanWhiteNoiseOfTheSameRms()
    {
        var pinkSamples = Pink(19937);
        var whiteSamples = White(19937);

        // Match RMS so the comparison is about spectral shape, not overall loudness.
        var scale = Analysis.Rms(pinkSamples) / Analysis.Rms(whiteSamples);
        for (var i = 0; i < whiteSamples.Length; i++)
        {
            whiteSamples[i] *= scale;
        }

        Assert.True(Analysis.SpectralCentroid(pinkSamples, SampleRate) < Analysis.SpectralCentroid(whiteSamples, SampleRate));
    }

    // The defining property, and the one a merely-darker filter fails: a 1/f spectrum puts the
    // same energy in every octave, so each band matches the one above it. White noise, whose
    // energy doubles with the bandwidth, comes out 3 dB below on the same measurement.
    [Theory]
    [InlineData(250.0f)]
    [InlineData(500.0f)]
    [InlineData(1000.0f)]
    [InlineData(2000.0f)]
    public void EnergyIsEqualInEveryOctave(float lower)
    {
        var pinkRatio = OctaveRatioDecibels(Pink(4242), lower);
        var whiteRatio = OctaveRatioDecibels(White(4242), lower);

        Assert.InRange(pinkRatio, -1.5, 1.5);
        Assert.InRange(whiteRatio, -4.0, -2.0);
    }

    [Fact]
    public void LevelMatchesWhiteNoiseItWasFilteredFrom()
    {
        Assert.InRange(Analysis.Rms(Pink(1861)) / Analysis.Rms(White(1861)), 0.9f, 1.1f);
    }

    private static double OctaveRatioDecibels(float[] samples, float lower)
    {
        var spectrum = Analysis.Fft(samples);
        var below = BandEnergy(spectrum, lower, lower * 2.0f);
        var above = BandEnergy(spectrum, lower * 2.0f, lower * 4.0f);
        return 10.0 * Math.Log10(below / above);
    }

    private static double BandEnergy(float[] spectrum, float lower, float upper)
    {
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

    private static float[] Pink(int seed)
    {
        var pink = new PinkNoise(new Rng(seed));
        var samples = new float[SampleCount];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = pink.Next();
        }

        return samples;
    }

    private static float[] White(int seed)
    {
        var white = new WhiteNoise(new Rng(seed));
        var samples = new float[SampleCount];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = white.Next();
        }

        return samples;
    }
}
