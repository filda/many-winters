using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class BiquadTests
{
    private const int SampleRate = 44100;

    [Fact]
    public void LowPassAttenuatesFourKilohertzAndPassesTwoHundredHertz()
    {
        Assert.InRange(GainDecibels(BiquadShape.LowPass, 1000.0f, 0.707f, 200.0f), -1.0, 1.0);
        Assert.True(GainDecibels(BiquadShape.LowPass, 1000.0f, 0.707f, 4000.0f) < -20.0);
    }

    [Fact]
    public void BandPassPassesItsCentreAndStopsAnOctaveAway()
    {
        Assert.InRange(GainDecibels(BiquadShape.BandPass, 900.0f, 4.0f, 900.0f), -1.0, 1.0);
        Assert.True(GainDecibels(BiquadShape.BandPass, 900.0f, 4.0f, 3600.0f) < -20.0);
    }

    [Fact]
    public void HighPassAttenuatesTwoHundredHertzAndPassesFourKilohertz()
    {
        Assert.InRange(GainDecibels(BiquadShape.HighPass, 1000.0f, 0.707f, 4000.0f), -1.0, 1.0);
        Assert.True(GainDecibels(BiquadShape.HighPass, 1000.0f, 0.707f, 200.0f) < -20.0);
    }

    [Fact]
    public void RetuningMovesThePassBand()
    {
        var biquad = new Biquad(SampleRate, BiquadShape.BandPass, 900.0f, 4.0f);
        Assert.True(Measure(biquad, 3600.0f) < -20.0);

        biquad.Retune(3600.0f, 4.0f);
        Assert.InRange(Measure(biquad, 3600.0f), -1.0, 1.0);
    }

    // A filter rebuilt instead of retuned restarts from a silent delay line, and the jump from
    // the last output sample to the first of the new block is the click that would be heard.
    [Fact]
    public void RetuningDoesNotJumpTheOutput()
    {
        var biquad = new Biquad(SampleRate, BiquadShape.BandPass, 600.0f, 2.0f);
        var noise = new WhiteNoise(new Rng(8123));

        var before = 0.0f;
        var largestStep = 0.0f;
        for (var i = 0; i < 4000; i++)
        {
            var current = biquad.Process(noise.Next());
            largestStep = MathF.Max(largestStep, MathF.Abs(current - before));
            before = current;
        }

        biquad.Retune(1400.0f, 2.0f);
        var after = biquad.Process(noise.Next());

        Assert.True(MathF.Abs(after - before) <= largestStep, $"retune stepped {MathF.Abs(after - before)}, more than the {largestStep} the filter already produces");
    }

    private static double GainDecibels(BiquadShape shape, float cutoff, float q, float frequency) =>
        Measure(new Biquad(SampleRate, shape, cutoff, q), frequency);

    // Steady-state RMS ratio of filtered to unfiltered sine, discarding the transient at the
    // start so the filter's own settling time doesn't distort the measurement.
    private static double Measure(Biquad biquad, float frequency)
    {
        const int length = 8192;
        const int settleSamples = 2048;

        var oscillator = new Oscillator(SampleRate, Waveform.Sine, frequency);
        var input = new float[length];
        var output = new float[length];
        for (var i = 0; i < length; i++)
        {
            input[i] = oscillator.Next();
            output[i] = biquad.Process(input[i]);
        }

        return 20.0 * Math.Log10(Analysis.Rms(output.AsSpan(settleSamples)) / Analysis.Rms(input.AsSpan(settleSamples)));
    }
}
