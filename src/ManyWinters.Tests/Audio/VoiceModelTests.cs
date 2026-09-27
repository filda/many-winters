using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class VoiceModelTests
{
    private const int SampleRate = 22050;

    // Oblique on purpose: none of these sit at 0, 1 or a round number, so no formula's arithmetic
    // quietly cancels.
    private static readonly VoiceIdentity Voice = new(PitchHz: 145.0f, Tract: 0.6f, Roughness: 0.35f, Breath: 0.4f);
    private static readonly Utterance Mutter = new(SyllableCount: 5, Pace: 3.1f, Energy: 0.22f);

    [Fact]
    public void SameSeedRendersAnIdenticalBuffer()
    {
        var first = VoiceModel.Render(Voice, Mutter, SampleRate, 613);
        var second = VoiceModel.Render(Voice, Mutter, SampleRate, 613);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentSeedsRenderDifferentBuffers()
    {
        var first = VoiceModel.Render(Voice, Mutter, SampleRate, 613);
        var second = VoiceModel.Render(Voice, Mutter, SampleRate, 614);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void EveryRenderNormalisesToPeak09Exactly()
    {
        var samples = VoiceModel.Render(Voice, Mutter, SampleRate, 42);

        Assert.Equal(0.9f, Analysis.Peak(samples), 4);
    }

    // DominantFrequency would report whichever formant peak happens to be loudest, not the
    // glottal rate. The fundamental instead has to be read off the low end of the spectrum, below
    // where any formant of this voice sits - a window comfortably above both test pitches but
    // below where the tract's own resonances start ringing.
    [Fact]
    public void HigherPitchHzRaisesTheMeasuredFundamental()
    {
        var low = VoiceModel.Render(Voice with { PitchHz = 95.0f }, Mutter, SampleRate, 1);
        var high = VoiceModel.Render(Voice with { PitchHz = 220.0f }, Mutter, SampleRate, 1);

        Assert.True(LowEndFundamental(high) > LowEndFundamental(low));
    }

    [Fact]
    public void LargerTractLowersTheSpectralCentroid()
    {
        var shortTract = VoiceModel.Render(Voice with { Tract = 0.1f }, Mutter, SampleRate, 613);
        var longTract = VoiceModel.Render(Voice with { Tract = 0.9f }, Mutter, SampleRate, 613);

        Assert.True(
            Analysis.SpectralCentroid(longTract, SampleRate) < Analysis.SpectralCentroid(shortTract, SampleRate));
    }

    // Counting syllables, not measuring a frequency - the same windowed-RMS hysteresis technique
    // CorvidCawTests and FrictionModelTests use for their own repeated events. A single threshold
    // would miscount on band-limited noise, blipping back over it inside one decaying syllable.
    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    public void SyllableCountSyllablesAreCountable(int syllableCount)
    {
        var utterance = Mutter with { SyllableCount = syllableCount };
        var samples = VoiceModel.Render(Voice, utterance, SampleRate, 9);

        Assert.Equal(syllableCount, CountSyllables(samples));
    }

    [Fact]
    public void RenderLengthFollowsSyllableCountOverPace()
    {
        var samples = VoiceModel.Render(Voice, Mutter, SampleRate, 11);

        var nominalSeconds = Mutter.SyllableCount / Mutter.Pace;
        var actualSeconds = samples.Length / (float)SampleRate;

        Assert.InRange(actualSeconds, nominalSeconds * 0.7f, nominalSeconds * 1.3f);
    }

    // Energy is effort, not articulation. Letting it scale vowel contrast was what made a mutter
    // come out as grunting in the previous, parallel-formant model - every syllable landed on the
    // same neutral formants - so contrast now has a high floor (see MovementMin) and Energy
    // drives the two things a listener actually takes for effort: how far the pitch steps between
    // syllables, and how voiced rather than breathy the source is.
    //
    // The waveguide changes how "more voiced" is measured, though. The previous model's parallel
    // formant bank left one bandpass free to dominate the spectrum, so a stronger harmonic
    // spectrum showed up as a single peak standing further above the mean. A real tract spreads
    // the same harmonic energy across several coupled resonances at once, so a tenser, less
    // breathy source instead reads as a *lower* peak-to-mean ratio and a higher one is what
    // breathier noise leaking through those same resonances gives - the opposite direction, for a
    // model built the opposite way around. Spectral flatness (the spectrum's geometric mean over
    // its arithmetic mean - 0 for a pure tone, 1 for white noise) measures the same underlying
    // fact, tonal versus noisy, without depending on how many peaks the tone happens to have:
    // measured, 0.023 for a call against 0.028 for a mutter, in the direction VoiceIdentity.Breath
    // and Glottis's own tenseness mapping intend.
    [Fact]
    public void ACallIsMoreVoicedAndLessBreathyThanAMutter()
    {
        var voice = new VoiceIdentity(PitchHz: 128.0f, Tract: 0.62f, Roughness: 0.35f, Breath: 0.7f);
        var shape = new Utterance(SyllableCount: 5, Pace: 2.7f, Energy: 0.15f);

        var mutter = VoiceModel.Render(voice, shape, SampleRate, 4113);
        var call = VoiceModel.Render(voice, shape with { Energy = 0.9f }, SampleRate, 4113);

        Assert.True(
            BestWindowFlatness(call) < BestWindowFlatness(mutter),
            $"mutter {BestWindowFlatness(mutter)}, call {BestWindowFlatness(call)}");
    }

    // A voice, not a rasp: the spectrum's peak-to-mean bin ratio has to sit well above white noise
    // of the same length, the same measurement CorvidCawTests uses in the other direction to prove
    // its caw is not tonal.
    [Fact]
    public void ItIsAVoiceNotARasp()
    {
        var samples = VoiceModel.Render(Voice, Mutter, SampleRate, 613);
        var noise = WhiteNoiseReference(samples.Length, 613);

        Assert.True(PeakToMeanRatio(samples) > PeakToMeanRatio(noise) * 2.0f);
    }

    private const float LowEndCeilingHz = 220.0f;

    private static float LowEndFundamental(float[] samples)
    {
        var spectrum = Analysis.Fft(samples);

        var peakBin = 0;
        var peakMagnitude = 0.0f;
        for (var bin = 0; bin < spectrum.Length; bin++)
        {
            var frequency = Analysis.BinFrequency(bin, spectrum.Length, SampleRate);
            if (frequency > LowEndCeilingHz)
            {
                break;
            }

            if (spectrum[bin] > peakMagnitude)
            {
                peakMagnitude = spectrum[bin];
                peakBin = bin;
            }
        }

        return Analysis.BinFrequency(peakBin, spectrum.Length, SampleRate);
    }

    private const float WindowSeconds = 0.02f;

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

    private static int CountSyllables(float[] samples)
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

    // The most tonal (least flat) any one syllable-sized window gets - the mirror image of
    // BestWindowPeakToMeanRatio in the previous version of this file, measuring the same "how
    // voiced is this syllable" question by spectral flatness instead, for the reason given on
    // ACallIsMoreVoicedAndLessBreathyThanAMutter.
    private static float BestWindowFlatness(float[] samples)
    {
        const int window = 2048;

        var best = float.MaxValue;
        for (var i = 0; i + window <= samples.Length; i += window / 2)
        {
            best = MathF.Min(best, SpectralFlatness(samples.AsSpan(i, window).ToArray()));
        }

        return best;
    }

    private static float SpectralFlatness(float[] samples)
    {
        var spectrum = Analysis.Fft(samples);

        double logSum = 0.0;
        double sum = 0.0;
        var count = 0;
        foreach (var magnitude in spectrum)
        {
            if (magnitude <= 0.0f)
            {
                continue;
            }

            logSum += Math.Log(magnitude);
            sum += magnitude;
            count++;
        }

        if (count == 0)
        {
            return 0.0f;
        }

        var geometricMean = Math.Exp(logSum / count);
        var arithmeticMean = sum / count;
        return (float)(geometricMean / arithmeticMean);
    }

    private static float PeakToMeanRatio(float[] samples)
    {
        var spectrum = Analysis.Fft(samples);
        return spectrum.Max() / spectrum.Average();
    }

    private static float[] WhiteNoiseReference(int length, int seed)
    {
        var noise = new WhiteNoise(new Rng(seed));
        var samples = new float[length];
        for (var i = 0; i < length; i++)
        {
            samples[i] = noise.Next();
        }

        return samples;
    }
}
