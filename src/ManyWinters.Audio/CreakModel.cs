namespace ManyWinters.Audio;

// Stick-slip, and the emphasis is on *stick*: fibres catch, hold, then let go, over and over.
// The sound is a run of short buzzes at irregular intervals, not one continuous tone. A first
// attempt glided a single sawtooth smoothly across the whole render and was heard as bungee
// jumping - which is exactly right, because a smooth pitch sweep is a slide whistle whatever
// timbre it wears. The rise has to arrive in jumps.
//
// Each slip is a brief sawtooth burst through a trunk-sized resonator bank. Both the buzz
// frequency and how often the slips come climb across the render as the cut deepens, so the
// tree audibly gets closer to going without anything ever sliding.
public static class CreakModel
{
    // Slips per second, at the start and at the end of the render. The acceleration is the
    // drama; the jitter on each interval is what stops it becoming a drum machine.
    private const float SlipsPerSecondStart = 4.0f;
    private const float SlipsPerSecondStrainRange = 7.0f;
    private const float SlipsPerSecondEndMultiplier = 2.4f;
    private const float SlipIntervalJitter = 0.45f;

    // The rate at which the fibre is ripping inside one slip. Size divides it, the same way it
    // divides the trunk's own.
    private const float BuzzBaseHz = 55.0f;
    private const float BuzzStrainRangeHz = 190.0f;
    private const float BuzzEndMultiplier = 1.5f;
    private const float BuzzJitter = 0.3f;

    // The one knob that decides rasp against note. A slip is a train of micro-catches: spaced
    // evenly they are a sawtooth and every slip is a plucked string - which is what a clean
    // oscillator here was heard as, rubber strings on a guitar. Spaced perfectly randomly they
    // are white noise. Jittered by about half, they are the inharmonic squawk of wood tearing.
    private const float ToothJitter = 0.5f;
    private const float ToothAmplitudeJitter = 0.35f;

    // Within one slip the tearing speeds up, so the catches crowd together towards its end.
    // Small: a large value bends the pitch, and a bent pitch is a rubber band.
    private const float SlipCrowding = 1.15f;

    // A slip is short. Long enough to have a pitch, far too short to be a note.
    private const float SlipMinSeconds = 0.022f;
    private const float SlipRangeSeconds = 0.075f;

    // The trunk, at a much lower base than any single struck body in ImpactModel: a creak is the
    // whole tree flexing, not one point of contact.
    private const float TrunkBaseHz = 220.0f;
    private const float TrunkSizeScale = 2.0f;
    private const float ModeDetuneRange = 0.06f;
    private const float ModeGainRollOffExponent = 0.3f;
    // Short. At a tenth of a second the bank still rings between slips and the whole thing sings
    // a chord; the trunk is meant to colour each catch, not to hold a note after it.
    private const float MinimumModeT60Seconds = 0.015f;
    private const float MaximumModeT60Seconds = 0.050f;

    // Mostly body. The raw catch train is a click track on its own, and loud it is the twang.
    private const float MixDirect = 0.25f;
    private const float MixBody = 1.0f;

    // Clear of the trunk's own modes, this only shaves the harsh upper harmonics a
    // non-band-limited sawtooth throws past audible trunk content.
    private const float ToneCutoffHz = 4000.0f;
    private const float ToneQ = 0.707f;

    // A swell, not an Envelope's percussive attack/decay: the creak rises as the fibres take up
    // load, holds while they tear, and falls back before the crack that follows takes over.
    private const float SwellRiseFraction = 1.0f / 3.0f;
    private const float SwellHoldFraction = 2.0f / 3.0f;

    private const float TargetRms = 0.2f;
    private const float TargetPeak = 0.9f;

    // Irregular and few - a handful of trunk modes, not the dense crowd ImpactModel needs to fake
    // a solid from a single impulse. The sawtooth already supplies its own dense harmonic series;
    // the bank only has to give it a body to ring inside.
    private static readonly float[] ModeRatios = [1.00f, 1.34f, 1.87f, 2.58f, 3.41f, 4.36f];

    public static float[] Render(Creak creak, float durationSeconds, int sampleRate, int seed)
    {
        var rng = new Rng(seed);
        var length = Math.Max((int)(durationSeconds * sampleRate), 1);

        var slips = new float[length];
        var slipsPerSecond = SlipsPerSecondStart + (SlipsPerSecondStrainRange * creak.Strain);
        var buzzHz = (BuzzBaseHz + (BuzzStrainRangeHz * creak.Strain)) / (1.0f + creak.Size);

        var at = 0;
        while (at < length)
        {
            // How far through the render this slip falls, which is how far through the cut the
            // tree is: everything speeds up and rises together from here.
            var progress = at / (float)length;
            var rate = slipsPerSecond * (1.0f + ((SlipsPerSecondEndMultiplier - 1.0f) * progress));
            var pitch = buzzHz * (1.0f + ((BuzzEndMultiplier - 1.0f) * progress)) * Jitter(rng, BuzzJitter);

            AddSlip(slips, at, pitch, SlipMinSeconds + (rng.NextFloat() * SlipRangeSeconds), sampleRate, rng);

            var interval = Jitter(rng, SlipIntervalJitter) / rate;
            at += Math.Max((int)(interval * sampleRate), 1);
        }

        var modes = BuildModes(creak.Size, rng);
        var body = new float[length];
        new ResonatorBank(sampleRate, modes).Process(slips, body);

        var tone = new Biquad(sampleRate, BiquadShape.LowPass, ToneCutoffHz, ToneQ);
        var mixed = new float[length];
        for (var i = 0; i < length; i++)
        {
            var p = length > 1 ? i / (float)(length - 1) : 1.0f;
            mixed[i] = tone.Process((MixDirect * slips[i]) + (MixBody * body[i])) * SwellEnvelope(p);
        }

        return Normalise(mixed);
    }

    // One catch-and-release, as a train of micro-catches at irregular spacing rather than an
    // oscillator. The irregularity is the whole point: it is what makes the burst inharmonic,
    // and inharmonic is the difference between wood tearing and a string being plucked.
    private static void AddSlip(float[] output, int onset, float frequency, float seconds, int sampleRate, Rng rng)
    {
        var samples = (int)(seconds * sampleRate);
        var at = 0;

        while (at < samples)
        {
            var index = onset + at;
            if (index >= output.Length)
            {
                return;
            }

            // Exponential from full to -60 dB across the slip, evaluated at the catch rather
            // than stepped per sample, since nothing between catches needs a value.
            var decay = MathF.Exp(-6.908f * at / samples);

            // Alternating sign at random. Same-sign catches would be more faithful to a release
            // that only goes one way, but they pile a DC offset into the direct mix that eats
            // headroom and thumps; the asymmetry that carries stick-slip is in each catch's
            // envelope, not in its polarity.
            var sign = rng.NextFloat() < 0.5f ? -1.0f : 1.0f;
            output[index] += sign * decay * Jitter(rng, ToothAmplitudeJitter);

            var crowding = 1.0f + ((SlipCrowding - 1.0f) * (at / (float)samples));
            var interval = Jitter(rng, ToothJitter) / (frequency * crowding);
            at += Math.Max((int)(interval * sampleRate), 1);
        }
    }

    private static float Jitter(Rng rng, float fraction) => 1.0f + ((rng.NextFloat() * 2.0f * fraction) - fraction);

    // A run of short bursts is spiky, so peak normalisation alone would leave it far too quiet -
    // the same levelling GranularModel and LiquidModel need, for the same reason.
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

    // Rises across the first third, holds, then eases off - the same raised-cosine shape
    // FrictionModel's stroke swell uses, stretched over the whole render instead of one stroke.
    private static float SwellEnvelope(float p)
    {
        if (p < SwellRiseFraction)
        {
            return 0.5f - (0.5f * MathF.Cos(MathF.PI * p / SwellRiseFraction));
        }

        if (p < SwellHoldFraction)
        {
            return 1.0f;
        }

        var fall = (p - SwellHoldFraction) / (1.0f - SwellHoldFraction);
        return 0.5f + (0.5f * MathF.Cos(MathF.PI * fall));
    }

    private static Mode[] BuildModes(float size, Rng rng)
    {
        var f0 = TrunkBaseHz / (1.0f + (TrunkSizeScale * size));

        var modes = new Mode[ModeRatios.Length];
        for (var i = 0; i < ModeRatios.Length; i++)
        {
            var detune = 1.0f + ((rng.NextFloat() * ModeDetuneRange) - (ModeDetuneRange / 2.0f));
            var frequency = f0 * ModeRatios[i] * detune;
            var t60 = MinimumModeT60Seconds + (rng.NextFloat() * (MaximumModeT60Seconds - MinimumModeT60Seconds));
            var gain = MathF.Pow(i + 1, -ModeGainRollOffExponent);

            modes[i] = new Mode(frequency, t60, gain);
        }

        return modes;
    }
}
