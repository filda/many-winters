namespace ManyWinters.Audio;

// A foot going into standing water. Three noise bands, each with its own decay - and that is the
// whole of it: the slap of the surface breaking, the spray falling back after it, and the slow
// water moving underneath. The granular model cannot do this, not because water is tonal but
// because a grain cloud has one envelope over one texture, where a splash is three things
// ending at three different times.
//
// An earlier pass built this around Minnaert bubbles - gliding tones, the textbook water cue,
// and the reason Oscillator.GlideTo exists. The ear threw them out: at any level where they
// were audible they read as a fizzing drink, and what carries the sound is the splash. They are
// gone rather than turned down, because a layer nothing is allowed to hear is not a layer.
public static class LiquidModel
{
    private const float BaseLengthSeconds = 0.35f;
    private const float DepthLengthSeconds = 0.25f;

    // The surface breaking: brief, and brighter the harder the water is hit.
    private const float SlapFrequencyBase = 900.0f;
    private const float SlapFrequencyVigourRange = 900.0f;
    private const float SlapQ = 0.8f;
    private const float SlapAttackSeconds = 0.002f;
    private const float SlapT60Seconds = 0.045f;
    private const float SlapMix = 0.9f;

    // The spray falling back, as a scatter of droplet ticks rather than a smooth band. Water
    // thrown up lands as separate drops, and that patter is what separates a splash from a
    // spade going into soil - which is what a continuous hiss in the same band was heard as.
    // Impulses, not tones: no bubbles.
    private const float SprayFrequencyHz = 3200.0f;
    private const float SprayQ = 1.2f;
    private const float SprayRateBase = 400.0f;
    private const float SprayRateVigourRange = 1600.0f;
    private const float SprayDropMinAmplitude = 0.25f;
    private const float SprayDropMaxAmplitude = 1.0f;
    private const float SprayAttackSeconds = 0.006f;
    private const float SprayT60BaseSeconds = 0.080f;
    private const float SprayT60VigourRangeSeconds = 0.170f;
    private const float SprayMix = 0.85f;

    // The water itself moving, which only a deep step really has.
    private const float DisplacementFrequencyBase = 200.0f;
    private const float DisplacementFrequencyShallowRange = 200.0f;
    private const float DisplacementQ = 0.707f;
    // Floored rather than proportional to Depth: even an inch of water has some weight in it,
    // and without the floor a shallow step was all spray and nothing under it.
    private const float DisplacementMix = 0.5f;
    private const float DisplacementDepthFloor = 0.3f;

    // No two splashes are the same one. Every band's frequency and the spray's decay are pulled
    // a little per render - without it three seeds came back within 14 Hz of each other, because
    // filtered noise under a fixed envelope has no character of its own to vary.
    private const float FrequencyJitter = 0.18f;
    private const float DecayJitter = 0.25f;

    // Every band here is two-pole, so each leaks most of the spectrum above its own centre and
    // the three together came out at a 3.9 kHz centroid - sizzle, not water. The same lowpass
    // the wind, granular and friction models all needed, and the same RMS levelling: a splash
    // is one sharp attack over a long quiet tail, so scaling that attack to full scale leaves
    // the rest 25 dB down.
    // Clear of the spray band, not on top of it: the first attempt put the cutoff at the spray's
    // own centre and took the wet patter out with the sizzle.
    private const float ToneCutoffHz = 5500.0f;
    private const float ToneQ = 0.707f;
    private const float TargetRms = 0.16f;
    private const float TargetPeak = 0.9f;

    public static float[] Render(LiquidSplash splash, int sampleRate, int seed)
    {
        var rng = new Rng(seed);

        var length = (int)((BaseLengthSeconds + (DepthLengthSeconds * splash.Depth)) * sampleRate);
        var mixed = new float[length];

        var slapHz = (SlapFrequencyBase + (SlapFrequencyVigourRange * splash.Vigour)) * Jitter(rng, FrequencyJitter);
        var sprayHz = SprayFrequencyHz * Jitter(rng, FrequencyJitter);
        var sprayT60 = (SprayT60BaseSeconds + (SprayT60VigourRangeSeconds * splash.Vigour)) * Jitter(rng, DecayJitter);
        var waterHz = (DisplacementFrequencyBase + (DisplacementFrequencyShallowRange * (1.0f - splash.Depth)))
            * Jitter(rng, FrequencyJitter);

        AddBand(
            mixed,
            rng,
            new Biquad(sampleRate, BiquadShape.BandPass, slapHz, SlapQ),
            new Envelope(sampleRate, SlapAttackSeconds, SlapT60Seconds),
            SlapMix);

        AddSpray(mixed, splash, rng, sampleRate, sprayHz, sprayT60);

        AddBand(
            mixed,
            rng,
            new Biquad(sampleRate, BiquadShape.LowPass, waterHz, DisplacementQ),
            new Envelope(sampleRate, SlapAttackSeconds, length / (float)sampleRate / 2.0f),
            DisplacementMix * (DisplacementDepthFloor + ((1.0f - DisplacementDepthFloor) * splash.Depth)));

        var tone = new Biquad(sampleRate, BiquadShape.LowPass, ToneCutoffHz, ToneQ);
        for (var i = 0; i < mixed.Length; i++)
        {
            mixed[i] = tone.Process(mixed[i]);
        }

        var rms = Analysis.Rms(mixed);
        if (rms <= 0.0f)
        {
            return mixed;
        }

        var drive = TargetRms / rms;
        for (var i = 0; i < mixed.Length; i++)
        {
            mixed[i] = MathF.Tanh(mixed[i] * drive);
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

    private static void AddSpray(float[] output, LiquidSplash splash, Rng rng, int sampleRate, float frequency, float t60)
    {
        var drops = new float[output.Length];
        new PoissonImpulses(
            rng,
            sampleRate,
            SprayRateBase + (SprayRateVigourRange * splash.Vigour),
            SprayDropMinAmplitude,
            SprayDropMaxAmplitude).Fill(drops);

        var filter = new Biquad(sampleRate, BiquadShape.BandPass, frequency, SprayQ);
        var envelope = new Envelope(sampleRate, SprayAttackSeconds, t60);
        var level = SprayMix * splash.Vigour;

        for (var i = 0; i < output.Length; i++)
        {
            output[i] += filter.Process(drops[i]) * envelope.Next() * level;
        }
    }

    private static float Jitter(Rng rng, float fraction) => 1.0f + ((rng.NextFloat() * 2.0f * fraction) - fraction);

    private static void AddBand(float[] output, Rng rng, Biquad filter, Envelope envelope, float level)
    {
        var noise = new WhiteNoise(rng);
        for (var i = 0; i < output.Length; i++)
        {
            output[i] += filter.Process(noise.Next()) * envelope.Next() * level;
        }
    }
}
