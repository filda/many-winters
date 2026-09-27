using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class AnalysisTests
{
    private const int SampleRate = 22050;

    [Fact]
    public void RmsAndPeakOnKnownInput()
    {
        float[] samples = [0.0f, 1.0f, -1.0f, 0.5f];

        Assert.Equal(1.0f, Analysis.Peak(samples));
        // sqrt((0^2 + 1^2 + 1^2 + 0.5^2) / 4) = sqrt(2.25 / 4) = 0.75.
        Assert.InRange(Analysis.Rms(samples), 0.74f, 0.76f);
    }

    [Fact]
    public void FftOfAPureSinePeaksAtItsBin()
    {
        const float frequency = 1234.0f;
        var oscillator = new Oscillator(SampleRate, Waveform.Sine, frequency);
        var samples = new float[4096];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = oscillator.Next();
        }

        var spectrum = Analysis.Fft(samples);

        var peakBin = 0;
        var peakMagnitude = 0.0f;
        for (var bin = 0; bin < spectrum.Length; bin++)
        {
            if (spectrum[bin] > peakMagnitude)
            {
                peakMagnitude = spectrum[bin];
                peakBin = bin;
            }
        }

        var peakFrequency = Analysis.BinFrequency(peakBin, spectrum.Length, SampleRate);
        Assert.InRange(peakFrequency, frequency - 20.0f, frequency + 20.0f);
    }

    [Fact]
    public void CentroidOfASineEqualsItsFrequencyWithinOneBin()
    {
        const float frequency = 900.0f;
        var oscillator = new Oscillator(SampleRate, Waveform.Sine, frequency);
        var samples = new float[4096];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = oscillator.Next();
        }

        var centroid = Analysis.SpectralCentroid(samples, SampleRate);
        var binWidth = SampleRate / (float)samples.Length;

        Assert.InRange(centroid, frequency - binWidth, frequency + binWidth);
    }

    [Fact]
    public void DominantFrequencyOfASine()
    {
        const float frequency = 3000.0f;
        var oscillator = new Oscillator(SampleRate, Waveform.Sine, frequency);
        var samples = new float[4096];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = oscillator.Next();
        }

        Assert.InRange(Analysis.DominantFrequency(samples, SampleRate), frequency - 20.0f, frequency + 20.0f);
    }

    [Fact]
    public void DurationAboveMeasuresALoudThenSilentSignal()
    {
        var samples = new float[SampleRate];
        for (var i = 0; i < SampleRate / 2; i++)
        {
            samples[i] = 1.0f;
        }

        var duration = Analysis.DurationAbove(samples, SampleRate, -40.0f);

        Assert.InRange(duration, 0.49f, 0.51f);
    }

    [Fact]
    public void BinFrequencyMapsFirstAndLastBins()
    {
        const int spectrumLength = 512;

        Assert.Equal(0.0f, Analysis.BinFrequency(0, spectrumLength, SampleRate));
        Assert.Equal(SampleRate / 2.0f, Analysis.BinFrequency(spectrumLength, spectrumLength, SampleRate), 0.01f);
    }
}
