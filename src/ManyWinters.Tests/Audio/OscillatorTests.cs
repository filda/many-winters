using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class OscillatorTests
{
    private const int SampleRate = 44100;

    [Fact]
    public void FourHundredFortyHertzSineCrossesZeroAboutEightHundredEightyTimesPerSecond()
    {
        var oscillator = new Oscillator(SampleRate, Waveform.Sine, 440.0f);
        var crossings = 0;
        var previous = oscillator.Next();
        for (var i = 1; i < SampleRate; i++)
        {
            var current = oscillator.Next();
            if (Math.Sign(current) != Math.Sign(previous) && Math.Sign(current) != 0)
            {
                crossings++;
            }

            previous = current;
        }

        Assert.InRange(crossings, 850, 910);
    }

    [Fact]
    public void SawtoothSpansTheUnitInterval()
    {
        var oscillator = new Oscillator(SampleRate, Waveform.Sawtooth, 233.0f);
        var samples = new float[SampleRate];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = oscillator.Next();
        }

        Assert.InRange(Analysis.Peak(samples), 0.95f, 1.0f);
        Assert.True(samples.Min() < -0.9f);
    }

    [Fact]
    public void GlideEndsAtTheTargetFrequency()
    {
        var oscillator = new Oscillator(SampleRate, Waveform.Sine, 220.0f);
        oscillator.GlideTo(660.0f, 0.2f);

        var samples = new float[SampleRate];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = oscillator.Next();
        }

        // Measure the frequency from the second half of the buffer, well after the 0.2 s glide
        // has finished, so the transient doesn't smear the FFT peak.
        var settled = samples.AsSpan(SampleRate / 2);
        var measured = Analysis.DominantFrequency(settled, SampleRate);

        Assert.InRange(measured, 630.0f, 690.0f);
    }
}
