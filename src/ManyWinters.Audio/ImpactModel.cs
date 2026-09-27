namespace ManyWinters.Audio;

// Modal synthesis for a single collision: a short noise burst excites two independent resonator
// banks, one per body, and the result mixes 0.4 striker / 0.6 struck - the struck body is what
// the ear tracks as "the thing that got hit".
public static class ImpactModel
{
    // Dense, irregular, and crowded at the bottom. Widely-spaced modes are the spectrum of a
    // hollow vessel - the first two tuning rounds were heard as a saucepan, then a plastic
    // bucket and a plastic pipe. Neighbouring low modes beat against each other instead of
    // fusing into a pitch, and no pitch is what tells the ear it is hearing a solid.
    private static readonly float[] ModeRatios =
    [
        1.00f, 1.19f, 1.37f, 1.62f, 1.83f, 2.11f, 2.34f, 2.67f,
        2.95f, 3.28f, 3.61f, 3.98f, 4.37f, 4.79f, 5.26f, 5.74f,
        6.29f, 6.87f, 7.51f, 8.19f, 8.94f, 9.75f, 10.60f, 11.60f,
    ];

    // Q, the quality factor, is the material property that actually holds still: it counts
    // oscillations, not seconds. T60 in seconds then falls out as ln(1000) * Q / (pi * f), which
    // damps every mode in proportion to its own frequency without a fudged exponent, and makes a
    // stiff, high-pitched body die sooner in absolute time than a low one of the same Q.
    private const float QualityFactorFloor = 3.0f;
    private const float QualityFactorRange = 25.0f;
    private const float DecayCyclesToT60 = 2.199f;

    // Bounds on the derived T60. A near-zero density drives f0 towards zero and the ring towards
    // minutes; neither bound is reachable with a plausible material.
    private const float MinimumT60Seconds = 0.005f;
    private const float MaximumT60Seconds = 0.8f;

    // Nearly flat, where a steeper roll-off leaves one loud fundamental ringing alone once the
    // transient has gone - which is exactly the sound of a struck plastic tube.
    private const float GainRollOffExponent = 0.25f;

    // A striker is held, and a hand is a large lossy mass against whatever it grips: it takes
    // several times the Q out of a body, which is why a bell is rung hanging and a hammer never
    // sings. Ringing both bodies freely made a mixed pair two pitched objects sounding at once.
    private const float StrikerQualityScale = 0.35f;

    private const float MixStriker = 0.4f;
    private const float MixStruck = 0.6f;

    // The unresonated contact noise, mixed back in. Every real impact has a component that never
    // enters either body; for stone on stone that noise is most of what says "two hard things
    // met", because there is barely any ring left to carry the message.
    private const float MixDirect = 0.18f;
    private const float TargetPeak = 0.9f;
    private const float MinimumLengthSeconds = 0.05f;
    private const float RingTailMultiplier = 1.2f;
    private const float BurstAttackSeconds = 0.0005f;
    private const float BiquadQ = 0.707f;

    public static float[] Render(ImpactMaterial striker, ImpactMaterial struck, float size, int sampleRate, int seed)
    {
        var rng = new Rng(seed);

        // Contact time belongs to the pair, not to either body: the two compliances add in
        // series, and for a stiff striker on a soft one that works out close to the mean. Taking
        // the softer body's hardness alone gave stone-on-wood the same long contact as
        // wood-on-wood, and a soft, slow contact is what made the mixed pair arrive as wood
        // into a plastic barrel. For a matched pair the mean is that pair's hardness, so
        // stone-on-stone and wood-on-wood come out bit-identical to before.
        var burstSeconds = 0.015f - (0.009f * (striker.Hardness + struck.Hardness) / 2.0f);

        // Brightness stays with the harder body. Wood's own modes all sit below 2 kHz, so this
        // cutoff only colours the direct contact noise - and that glare is the main thing
        // saying "stone" when the struck body is doing all the ringing.
        var burstCutoff = 1000.0f + (7000.0f * MathF.Max(striker.Hardness, struck.Hardness));

        var strikerModes = BuildModes(striker, size, rng, StrikerQualityScale);
        var struckModes = BuildModes(struck, size, rng, qualityScale: 1.0f);

        var strikerT60 = ModeT60(strikerModes);
        var struckT60 = ModeT60(struckModes);

        var lengthSeconds = MathF.Max(burstSeconds + (RingTailMultiplier * MathF.Max(strikerT60, struckT60)), MinimumLengthSeconds);
        var length = (int)(lengthSeconds * sampleRate);

        var excitation = new float[length];
        var burstSamples = (int)(burstSeconds * sampleRate);
        var burstEnvelope = new Envelope(sampleRate, BurstAttackSeconds, burstSeconds / 3.0f);
        var noise = new WhiteNoise(rng);
        for (var i = 0; i < burstSamples; i++)
        {
            excitation[i] = noise.Next() * burstEnvelope.Next();
        }

        var burstLowpass = new Biquad(sampleRate, BiquadShape.LowPass, burstCutoff, BiquadQ);
        for (var i = 0; i < excitation.Length; i++)
        {
            excitation[i] = burstLowpass.Process(excitation[i]);
        }

        var strikerBank = new ResonatorBank(sampleRate, strikerModes);
        var strikerOutput = new float[length];
        strikerBank.Process(excitation, strikerOutput);

        var struckBank = new ResonatorBank(sampleRate, struckModes);
        var struckOutput = new float[length];
        struckBank.Process(excitation, struckOutput);

        var mixed = new float[length];
        for (var i = 0; i < length; i++)
        {
            mixed[i] = (MixDirect * excitation[i]) + (MixStriker * strikerOutput[i]) + (MixStruck * struckOutput[i]);
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

    private static Mode[] BuildModes(ImpactMaterial body, float size, Rng rng, float qualityScale)
    {
        // Hardness raised to 2.5 rather than kept linear: it has to separate stone from wood
        // hard enough for pitch alone to identify the material, without dragging wood up with
        // it, and wood is the pairing that already sounded right. Stone lands near 880 Hz and
        // wood near 180 Hz.
        var f0 = 110.0f * MathF.Sqrt(body.Density) * MathF.Pow(1.0f + body.Hardness, 2.5f) / size;

        // Toughness is loss, so it sets Q alone. Hardness no longer touches the decay directly:
        // it raises f0, and a higher f0 at the same Q already rings for less time.
        var qualityFactor = (QualityFactorFloor + (QualityFactorRange * (1.0f - body.Toughness))) * qualityScale;

        var modes = new Mode[ModeRatios.Length];
        for (var i = 0; i < ModeRatios.Length; i++)
        {
            var detune = 1.0f + ((rng.NextFloat() * 0.06f) - 0.03f);
            var frequency = f0 * ModeRatios[i] * detune;
            var t60 = Math.Clamp(DecayCyclesToT60 * qualityFactor / frequency, MinimumT60Seconds, MaximumT60Seconds);

            // An uneven roll-off rather than a clean curve: mode strengths that fall away in an
            // exactly regular line are the other half of the plastic sound. The fundamental is
            // left alone, because it carries the pitch the ear sorts materials by - randomising
            // it too put one seed of wood inside stone's range.
            var spread = i == 0 ? 1.0f : 0.7f + (0.6f * rng.NextFloat());
            var gain = MathF.Pow(i + 1, -GainRollOffExponent) * spread;

            modes[i] = new Mode(frequency, t60, gain);
        }

        return modes;
    }

    // The mode T60s already fold in the per-index roll-off; the body's own ring length for the
    // overall buffer is set by its longest-lived (lowest-index) mode.
    private static float ModeT60(IReadOnlyList<Mode> modes) => modes[0].T60Seconds;
}
