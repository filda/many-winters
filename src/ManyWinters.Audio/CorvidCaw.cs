namespace ManyWinters.Audio;

// A caw is a voice, not a hit. Three things separate it from a thump, and the first version had
// none of them.
//
// **Less jitter than wood.** The pulse train is CreakModel's trick with the interval jitter
// turned down from a half to a sixth. Measured afterwards: that alone does not preserve a
// pitch here, because the random per-pulse sign inherited from the creak flattens the spectrum
// on its own - the line at the pulse rate stands 1.6x over the mean bin with the random sign
// against 6.1x without it. The creak needs that sign to keep a DC offset out of its direct mix;
// a caw does not, since its formants are bandpasses. It is left as it is because the ear passed
// this call, but what carries it is the formant contour, the breath and the plateau below, not
// a surviving pitch. VoiceModel, which does need a recoverable fundamental, uses one polarity.
//
// **A plateau, not a decay.** A crow holds "caa" at level for a fifth of a second and stops. An
// exponential decay is the envelope of something being struck, and it reads that way whatever
// is underneath it.
//
// **A contour.** The pitch falls and the formants close through the call - that fall is the
// "aw". Held still, the call has no mouth in it.
public static class CorvidCaw
{
    // A crow caws far higher than a first guess suggests: around 500 Hz, with a raven down near
    // 280. Set at 130-250 the train sits squarely in the band where a buzz is heard as
    // flatulence, whatever formants are stacked on top of it.
    private const float PulseRateMinHz = 240.0f;
    private const float PulseRateMaxHz = 700.0f;

    // The one number that decides voice against noise. See the note above.
    private const float PulseIntervalJitter = 0.16f;
    private const float PulseAmplitudeJitter = 0.3f;

    // The fall belongs at the end of the call, not spread across it. A pitch sliding evenly
    // from the first sample to the last, with both formants sliding under it, is a descending
    // sweep of everything at once - which is a laser, and was heard as one. A corvid holds the
    // note and closes at the last moment.
    private const float ContourHoldFraction = 0.55f;
    private const float PitchFallFraction = 0.08f;

    // Formants scale down with size, so a big bird is not just slower but lower-throated, and
    // they close as the call ends - the mouth shutting on the "aw".
    private const float Formant1StartHz = 1300.0f;
    private const float Formant1EndHz = 950.0f;
    private const float Formant2StartHz = 2900.0f;
    private const float Formant2EndHz = 2200.0f;
    private const float SizeScale = 1.2f;
    private const float FormantQ = 3.0f;

    // Retuned once per block rather than per sample: at a contour this slow the difference is
    // inaudible and the trig is not.
    private const int ControlBlock = 64;

    private const float AttackSeconds = 0.012f;
    private const float HoldFraction = 0.72f;
    private const float CawMinSeconds = 0.18f;
    private const float CawRangeSeconds = 0.14f;

    private const int MinimumRepeats = 1;
    private const int RepeatRange = 3; // draws 0..2, so 1..3 repeats total
    private const float GapMinSeconds = 0.08f;
    private const float GapRangeSeconds = 0.12f;

    // Clear of both formants, so it only shaves the harshest edge a non-band-limited pulse train
    // throws above them - the same placement rule every model here follows.
    // A caw is more breath than voice. A pulse train alone is a buzz however it is filtered;
    // the aperiodic half is what the ear takes as a throat rather than a reed.
    private const float BreathMix = 0.45f;

    private const float ToneCutoffHz = 5200.0f;
    private const float ToneQ = 0.707f;

    private const float TargetRms = 0.18f;
    private const float TargetPeak = 0.9f;

    public static float[] Render(float size, int sampleRate, int seed)
    {
        var rng = new Rng(seed);

        var pulseRateHz = PulseRateMaxHz - ((PulseRateMaxHz - PulseRateMinHz) * size);
        var sizeScale = 1.0f / (1.0f + (SizeScale * size));

        var repeats = MinimumRepeats + (int)(rng.NextFloat() * RepeatRange);
        var caws = new (float[] Samples, int Onset)[repeats];

        var cursor = 0;
        var length = 0;
        for (var c = 0; c < repeats; c++)
        {
            var seconds = CawMinSeconds + (rng.NextFloat() * CawRangeSeconds);
            var caw = RenderOneCaw(pulseRateHz, sizeScale, seconds, sampleRate, rng);
            caws[c] = (caw, cursor);
            length = Math.Max(length, cursor + caw.Length);
            cursor += caw.Length + (int)((GapMinSeconds + (rng.NextFloat() * GapRangeSeconds)) * sampleRate);
        }

        var mixed = new float[length];
        foreach (var (samples, onset) in caws)
        {
            for (var i = 0; i < samples.Length; i++)
            {
                mixed[onset + i] += samples[i];
            }
        }

        return Normalise(mixed);
    }

    private static float[] RenderOneCaw(
        float pulseRateHz, float sizeScale, float seconds, int sampleRate, Rng rng)
    {
        var length = Math.Max((int)(seconds * sampleRate), 1);

        var pulses = new float[length];
        var at = 0;
        while (at < length)
        {
            var sign = rng.NextFloat() < 0.5f ? -1.0f : 1.0f;
            pulses[at] += sign * Jitter(rng, PulseAmplitudeJitter);

            var fall = 1.0f - (PitchFallFraction * Contour(at / (float)length));
            var interval = Jitter(rng, PulseIntervalJitter) / (pulseRateHz * fall);
            at += Math.Max((int)(interval * sampleRate), 1);
        }

        var filter1 = new Biquad(sampleRate, BiquadShape.BandPass, Formant1StartHz * sizeScale, FormantQ);
        var filter2 = new Biquad(sampleRate, BiquadShape.BandPass, Formant2StartHz * sizeScale, FormantQ);
        var tone = new Biquad(sampleRate, BiquadShape.LowPass, ToneCutoffHz, ToneQ);
        var breath = new WhiteNoise(rng);

        var caw = new float[length];
        for (var i = 0; i < length; i++)
        {
            if (i % ControlBlock == 0)
            {
                var p = Contour(i / (float)length);
                filter1.Retune(Lerp(Formant1StartHz, Formant1EndHz, p) * sizeScale, FormantQ);
                filter2.Retune(Lerp(Formant2StartHz, Formant2EndHz, p) * sizeScale, FormantQ);
            }

            // The breath goes through the same formants, so it is the same throat rather than
            // a hiss laid over the top of one.
            var source = pulses[i] + (BreathMix * breath.Next());
            var formed = filter1.Process(source) + filter2.Process(source);
            caw[i] = tone.Process(formed) * PlateauEnvelope(i / (float)length, seconds);
        }

        return caw;
    }

    // Quick on, hold, quick off. Anything with a decay on it is a struck object.
    private static float PlateauEnvelope(float p, float seconds)
    {
        var attackFraction = MathF.Min(AttackSeconds / seconds, HoldFraction);
        if (p < attackFraction)
        {
            return p / attackFraction;
        }

        if (p < HoldFraction)
        {
            return 1.0f;
        }

        return 1.0f - ((p - HoldFraction) / (1.0f - HoldFraction));
    }

    // Flat until the call is more than half done, then the close. Both the pitch and the
    // formants ride this, so the whole contour happens where a beak actually shuts.
    private static float Contour(float p) =>
        p < ContourHoldFraction ? 0.0f : (p - ContourHoldFraction) / (1.0f - ContourHoldFraction);

    private static float Lerp(float from, float to, float p) => from + ((to - from) * p);

    private static float Jitter(Rng rng, float fraction) => 1.0f + ((rng.NextFloat() * 2.0f * fraction) - fraction);

    // A run of pulse bursts is spiky, so peak normalisation alone would leave it too quiet - the
    // same levelling every impulse-built model here needs.
    private static float[] Normalise(float[] samples)
    {
        var rms = Analysis.Rms(samples);
        if (rms <= 0.0f)
        {
            return samples;
        }

        var drive = TargetRms / rms;
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = MathF.Tanh(samples[i] * drive);
        }

        var peak = Analysis.Peak(samples);
        if (peak <= 0.0f)
        {
            return samples;
        }

        var scale = TargetPeak / peak;
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] *= scale;
        }

        return samples;
    }
}
