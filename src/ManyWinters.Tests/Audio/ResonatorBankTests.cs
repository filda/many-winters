using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class ResonatorBankTests
{
    private const int SampleRate = 22050;

    [Fact]
    public void ImpulseResponsePeaksAtEachConfiguredFrequency()
    {
        Mode[] modes =
        [
            new Mode(600.0f, 0.3f, 1.0f),
            new Mode(2200.0f, 0.3f, 1.0f),
        ];

        var bank = new ResonatorBank(SampleRate, modes);
        var excitation = new float[8192];
        excitation[0] = 1.0f;
        var output = new float[excitation.Length];

        bank.Process(excitation, output);

        var spectrum = Analysis.Fft(output);

        // Each mode's own energy dwarfs the other's at its neighbouring bins, even though the
        // two modes' peak magnitudes differ from each other - a local-peak check, not a
        // whole-spectrum argmax.
        AssertLocalPeakNear(spectrum, 600.0f);
        AssertLocalPeakNear(spectrum, 2200.0f);
    }

    [Fact]
    public void SingleModeDecaysToSixtyDecibelsDownAtT60()
    {
        const float t60Seconds = 0.4f;
        Mode[] modes = [new Mode(500.0f, t60Seconds, 1.0f)];

        var bank = new ResonatorBank(SampleRate, modes);
        var t60Samples = (int)(t60Seconds * SampleRate);
        var excitation = new float[t60Samples + 200];
        excitation[0] = 1.0f;
        var output = new float[excitation.Length];

        bank.Process(excitation, output);

        // The pole radius only guarantees a -60 dB drop between two points T60 apart, not
        // relative to sample zero: the resonance takes a few cycles to build to its own peak
        // before the exponential envelope actually starts decaying from there.
        var peakIndex = 0;
        var peakMagnitude = 0.0f;
        for (var i = 0; i < 200; i++)
        {
            if (MathF.Abs(output[i]) > peakMagnitude)
            {
                peakMagnitude = MathF.Abs(output[i]);
                peakIndex = i;
            }
        }

        var atT60 = MathF.Abs(output[peakIndex + t60Samples]);

        Assert.InRange(atT60 / peakMagnitude, 0.0009f, 0.0011f);
    }

    [Fact]
    public void ProcessAddsIntoOutputRatherThanOverwriting()
    {
        Mode[] modes = [new Mode(500.0f, 0.2f, 1.0f)];
        var bank = new ResonatorBank(SampleRate, modes);

        var excitation = new float[100];
        excitation[0] = 1.0f;

        var fromZero = new float[excitation.Length];
        bank.Process(excitation, fromZero);

        // Priming the output with a constant before Process must leave that constant added into
        // every sample, not replaced - the same computation as fromZero, offset by the prime.
        const float prime = 0.42f;
        var primed = new float[excitation.Length];
        Array.Fill(primed, prime);
        bank.Process(excitation, primed);

        for (var i = 0; i < primed.Length; i++)
        {
            Assert.Equal(fromZero[i] + prime, primed[i], 0.0001f);
        }
    }

    private static void AssertLocalPeakNear(float[] spectrum, float expectedFrequency)
    {
        const float window = 150.0f;

        var peakBin = 0;
        var peakMagnitude = 0.0f;
        for (var bin = 0; bin < spectrum.Length; bin++)
        {
            var frequency = Analysis.BinFrequency(bin, spectrum.Length, SampleRate);
            if (MathF.Abs(frequency - expectedFrequency) <= window && spectrum[bin] > peakMagnitude)
            {
                peakMagnitude = spectrum[bin];
                peakBin = bin;
            }
        }

        var peakFrequency = Analysis.BinFrequency(peakBin, spectrum.Length, SampleRate);
        Assert.InRange(peakFrequency, expectedFrequency - 60.0f, expectedFrequency + 60.0f);

        var farBin = Math.Clamp(peakBin + (int)(400.0f / Analysis.BinFrequency(1, spectrum.Length, SampleRate)), 0, spectrum.Length - 1);
        Assert.True(peakMagnitude > spectrum[farBin] * 2.0f);
    }
}
