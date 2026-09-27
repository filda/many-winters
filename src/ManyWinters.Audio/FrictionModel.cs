namespace ManyWinters.Audio;

// A stone sharpened against another stone: a swell per stroke, not a burst, because the gesture is
// the hand accelerating into the stone and easing off, not an impact. Two continuous noise layers
// (abrasion, grit) are gated by that swell rather than re-triggered per stroke, which is what keeps
// the texture underneath from sounding like the same sample looped.
public static class FrictionModel
{
    // Short silence beyond the last stroke's slot so the tail of its swell is never clipped by the
    // buffer edge.
    private const float TailSeconds = 0.15f;

    // The swell rises across the first 35% of its slot and falls across the rest, reaching
    // near-silence before the next stroke starts - a hand easing off a stone, not a hammer.
    private const float RiseFraction = 0.35f;

    // The swell only fills this much of its slot; the remainder is the gap that makes the stroke
    // count countable by RMS instead of one continuous hiss.
    private const float ActiveFraction = 0.85f;

    // Five identical strokes is the clearest possible tell of synthesis - every stroke gets its own
    // small pull from Rng, on top of the shared shape.
    private const float DurationJitterFraction = 0.06f;
    private const float LevelJitterFraction = 0.12f;

    // The hiss of the two surfaces passing. Finer grit is brighter: less material is being torn
    // per pass, so the noise it makes sits higher.
    private const float AbrasionFrequencyBase = 700.0f;
    private const float AbrasionFrequencyGritScale = 2300.0f;
    private const float AbrasionQ = 0.7f;

    // A two-pole bandpass falls away at only 6 dB/octave above its centre, so on white noise it
    // leaves most of the top end intact and the result reads as hiss rather than as stone. The
    // wind model arrived at the same wall and the same fix: a lowpass over the band, moving with
    // it, so brightness tracks grit instead of being a constant wash.
    private const float ToneFrequencyBase = 1600.0f;
    private const float ToneFrequencyGritScale = 4200.0f;
    private const float ToneQ = 0.707f;

    // Individual particles under the pressure. Coarse grit means fewer, bigger particles catching
    // at a time, so the rate falls as Grit rises even though the abrasion layer brightens.
    private const float GritRateBase = 400.0f;
    private const float GritRateRange = 2600.0f;
    private const float GritFrequencyBase = 1400.0f;
    private const float GritFrequencyGritScale = 1600.0f;
    private const float GritQ = 1.5f;
    private const float GritMinimumAmplitude = 0.3f;
    private const float GritMaximumAmplitude = 1.0f;
    private const float GritMixUnderAbrasion = 0.5f;

    // Weight under a heavy stroke: the same noise, low-passed, added in proportion to Pressure
    // rather than always present, so a light stroke stays all hiss.
    private const float PressureLowpassFrequency = 400.0f;
    private const float PressureLowpassQ = 0.7f;
    private const float PressureLowpassGain = 0.4f;

    private const float TargetPeak = 0.9f;

    public static float[] Render(FrictionStroke stroke, int strokeCount, int sampleRate, int seed)
    {
        var rng = new Rng(seed);

        var slotSamples = (int)(sampleRate / stroke.StrokesPerSecond);
        var length = (strokeCount * slotSamples) + (int)(TailSeconds * sampleRate);

        var durationJitter = new float[strokeCount];
        var levelJitter = new float[strokeCount];
        for (var s = 0; s < strokeCount; s++)
        {
            durationJitter[s] = 1.0f + (((rng.NextFloat() * 2.0f) - 1.0f) * DurationJitterFraction);
            levelJitter[s] = 1.0f + (((rng.NextFloat() * 2.0f) - 1.0f) * LevelJitterFraction);
        }

        var abrasion = BuildAbrasionLayer(stroke, rng, sampleRate, length);
        var grit = BuildGritLayer(stroke, rng, sampleRate, length);
        var pressureLow = BuildPressureLayer(rng, sampleRate, length);

        // Filtered across the whole buffer before the swell gates it. A filter stepped only
        // during the strokes would skip the gaps, and its state would arrive at each stroke
        // holding the end of the previous one - a click on every attack.
        var tone = new Biquad(sampleRate, BiquadShape.LowPass, ToneFrequencyBase + (ToneFrequencyGritScale * stroke.Grit), ToneQ);
        var high = new float[length];
        for (var i = 0; i < length; i++)
        {
            high[i] = tone.Process(abrasion[i] + (GritMixUnderAbrasion * grit[i]));
        }

        var mixed = new float[length];
        for (var i = 0; i < length; i++)
        {
            var strokeIndex = i / slotSamples;
            if (strokeIndex >= strokeCount)
            {
                continue;
            }

            var localSample = i - (strokeIndex * slotSamples);
            var activeSamples = slotSamples * ActiveFraction * durationJitter[strokeIndex];
            var p = localSample / activeSamples;
            if (p >= 1.0f)
            {
                continue;
            }

            var swell = SwellEnvelope(p) * levelJitter[strokeIndex];
            mixed[i] = swell * (high[i] + (PressureLowpassGain * stroke.Pressure * pressureLow[i]));
        }

        var peak = Analysis.Peak(mixed);
        if (peak <= 0.0f)
        {
            return mixed;
        }

        var scale = TargetPeak / peak;
        for (var i = 0; i < mixed.Length; i++)
        {
            mixed[i] *= scale;
        }

        return mixed;
    }

    // Raised sine, shaped asymmetrically: a fast rise into the stone and a slower ease off it,
    // rather than the percussive attack/decay Envelope gives an impact.
    private static float SwellEnvelope(float p)
    {
        if (p < RiseFraction)
        {
            return 0.5f - (0.5f * MathF.Cos(MathF.PI * p / RiseFraction));
        }

        var fall = (p - RiseFraction) / (1.0f - RiseFraction);
        return 0.5f + (0.5f * MathF.Cos(MathF.PI * fall));
    }

    private static float[] BuildAbrasionLayer(FrictionStroke stroke, Rng rng, int sampleRate, int length)
    {
        var frequency = AbrasionFrequencyBase + (AbrasionFrequencyGritScale * stroke.Grit);
        var noise = new WhiteNoise(rng);
        var filter = new Biquad(sampleRate, BiquadShape.BandPass, frequency, AbrasionQ);

        var layer = new float[length];
        for (var i = 0; i < length; i++)
        {
            layer[i] = filter.Process(noise.Next());
        }

        return layer;
    }

    private static float[] BuildGritLayer(FrictionStroke stroke, Rng rng, int sampleRate, int length)
    {
        var rate = GritRateBase + (GritRateRange * (1.0f - stroke.Grit));
        var frequency = GritFrequencyBase + (GritFrequencyGritScale * stroke.Grit);

        var layer = new float[length];
        var impulses = new PoissonImpulses(rng, sampleRate, rate, GritMinimumAmplitude, GritMaximumAmplitude);
        impulses.Fill(layer);

        var filter = new Biquad(sampleRate, BiquadShape.BandPass, frequency, GritQ);
        for (var i = 0; i < length; i++)
        {
            layer[i] = filter.Process(layer[i]);
        }

        return layer;
    }

    private static float[] BuildPressureLayer(Rng rng, int sampleRate, int length)
    {
        var noise = new WhiteNoise(rng);
        var filter = new Biquad(sampleRate, BiquadShape.LowPass, PressureLowpassFrequency, PressureLowpassQ);

        var layer = new float[length];
        for (var i = 0; i < length; i++)
        {
            layer[i] = filter.Process(noise.Next());
        }

        return layer;
    }
}
