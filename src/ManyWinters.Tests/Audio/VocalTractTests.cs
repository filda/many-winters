using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class VocalTractTests
{
    private const int SampleRate = 22050;

    // Upstream's own resting shape (TractShaper's constructor defaults), used here as "a neutral
    // vowel" for exactly the reason VoiceModel's own NeutralShape comment gives: it is Pink
    // Trombone's own choice of what a tract sits at when nothing asks it to do anything in
    // particular, not an average this port invented.
    private static readonly TractShape Neutral = new(TongueIndex: 12.9f, TongueDiameter: 2.43f, LipDiameter: 1.5f);

    [Fact]
    public void LongRunAtExtremeShapesStaysFiniteAndBounded()
    {
        var rng = new Rng(2027);
        var tract = new VocalTract(SampleRate);

        // Two shapes near the extreme ends of what VoiceModel ever asks for: about as open as the
        // vowel table gets, and about as narrow (short of an outright closure, covered below).
        var open = new TractShape(TongueIndex: 21.0f, TongueDiameter: 3.3f, LipDiameter: 1.5f);
        var narrow = new TractShape(TongueIndex: 30.0f, TongueDiameter: 1.6f, LipDiameter: 0.7f);

        var maxAbs = 0.0f;
        for (var i = 0; i < 100_000; i++)
        {
            if (i % 256 == 0)
            {
                tract.SetShape(i % 512 == 0 ? open : narrow);
            }

            var excitation = (rng.NextFloat() - 0.5f) * 0.2f;
            var sample = tract.Step(excitation, (i % 256) / 256.0f);

            Assert.True(float.IsFinite(sample), $"non-finite sample at i={i}");
            maxAbs = MathF.Max(maxAbs, MathF.Abs(sample));
        }

        // A bound loose enough to allow a loud resonance, tight enough to catch the runaway a
        // moving near-total closure produced before VoiceModel restricted consonants to the lips
        // (see VoiceModel's own comment on why) - that failure mode reached floating-point
        // infinity within a couple of thousand samples, nowhere near this bound.
        Assert.True(maxAbs < 100.0f, $"max abs sample {maxAbs} suggests a runaway, not a resonance");
    }

    // TongueDiameter is the width of the section the tongue's cosine hump carries - see
    // VocalTract.RestDiameter. Widening it (towards an open vowel) lowers the resonance the same
    // way a wider real vocal-tract section lowers the frequency it resonates at.
    [Fact]
    public void WiderTractSectionLowersTheResonanceItProduces()
    {
        var narrow = Neutral with { TongueDiameter = 1.8f };
        var wide = Neutral with { TongueDiameter = 3.2f };

        Assert.True(
            ImpulseResponseCentroid(wide) < ImpulseResponseCentroid(narrow),
            $"wide centroid {ImpulseResponseCentroid(wide)} was not below narrow's {ImpulseResponseCentroid(narrow)}");
    }

    [Fact]
    public void ClosingAConstrictionToZeroSilencesTheOutput()
    {
        var tract = new VocalTract(SampleRate);
        var closed = Neutral with { LipDiameter = 0.0f };
        tract.SetShape(closed);
        tract.SetShape(closed);

        var samples = new float[10_000];
        for (var i = 0; i < samples.Length; i++)
        {
            if (i % 64 == 0)
            {
                tract.SetShape(closed);
            }

            // A steady, low-amplitude excitation stands in for a glottal pulse train without
            // pulling Glottis into a test that is about the tract's own boundary, not the source.
            var excitation = MathF.Sin(2.0f * MathF.PI * 120.0f * i / SampleRate) * 0.3f;
            samples[i] = tract.Step(excitation, (i % 64) / 64.0f);
        }

        // Not exactly zero: the nose branch still couples a whisker of energy through even a
        // sealed mouth (a real closed mouth still has a nose), and the sealed cavity itself rings
        // briefly on the way to silence. What a closure has to do is come down to silence, not
        // start there.
        Assert.True(
            Analysis.Rms(samples.AsSpan(samples.Length / 2, samples.Length / 2)) < 0.01f,
            "a sustained closure should have settled to near-silence by the second half of the run");
    }

    // The test the port lives or dies by: a neutral vowel's resonances should land where a
    // textbook expects a vowel's formants to sit, not scattered arbitrarily. Measured off an
    // impulse response - a single excitation and its decay - rather than a periodic source, whose
    // own harmonics would otherwise dominate the peak-picking (see the plan's own note on this).
    [Fact]
    public void NeutralShapeResonancesLandInPlausibleFormantRanges()
    {
        var peaks = ImpulseResponsePeaks(Neutral, count: 4);

        Assert.Contains(peaks, f => f is > 800.0f and < 1800.0f);
        Assert.Contains(peaks, f => f is > 1800.0f and < 2600.0f);
        Assert.Contains(peaks, f => f is > 2600.0f and < 3600.0f);
    }

    // Two calls to SetShape with the same shape settle the block-interpolation immediately (see
    // VocalTract's own comment on why Step interpolates at all) instead of measuring a response
    // still gliding in from whatever the tract last held.
    private static float[] ImpulseResponse(TractShape shape)
    {
        var tract = new VocalTract(SampleRate);
        tract.SetShape(shape);
        tract.SetShape(shape);

        const int length = 2048;
        var samples = new float[length];
        samples[0] = tract.Step(1.0f, 1.0f);
        for (var i = 1; i < length; i++)
        {
            samples[i] = tract.Step(0.0f, 1.0f);
        }

        return samples;
    }

    private static float ImpulseResponseCentroid(TractShape shape) =>
        Analysis.SpectralCentroid(ImpulseResponse(shape), SampleRate);

    private static float[] ImpulseResponsePeaks(TractShape shape, int count)
    {
        var spectrum = Analysis.Fft(ImpulseResponse(shape));

        var peaks = new List<(float Frequency, float Magnitude)>();
        for (var i = 2; i < spectrum.Length - 2; i++)
        {
            if (spectrum[i] >= spectrum[i - 1] && spectrum[i] >= spectrum[i + 1] &&
                spectrum[i] >= spectrum[i - 2] && spectrum[i] >= spectrum[i + 2])
            {
                peaks.Add((Analysis.BinFrequency(i, spectrum.Length, SampleRate), spectrum[i]));
            }
        }

        peaks.Sort((a, b) => b.Magnitude.CompareTo(a.Magnitude));
        return peaks.Take(count).Select(p => p.Frequency).ToArray();
    }
}
