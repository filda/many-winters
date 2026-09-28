namespace ManyWinters.Audio;

// What was done to the surface, as opposed to what the surface is. Orthogonal to it on purpose:
// the same soil is dug, walked on and scraped.
public readonly record struct GranularGesture(float AttackSeconds, float T60Seconds, float Intensity);

// Grain-cloud synthesis: a Poisson stream of tiny impacts, filtered for brightness and used to
// drive both a small resonant body and a dry noise floor. A footstep, a dig and a handful of
// leaves are this same engine at different rates and dampings, not three different models.
public static class GranularModel
{
    private const float GrainMinimumAmplitude = 0.3f;
    private const float GrainMaximumAmplitude = 1.0f;

    // Brightness lives in the stream, not in any one grain: an isolated impulse has no timbre of
    // its own, so this bandpass is what makes a hard surface sound sharper than a soft one.
    private const float GrainFilterBaseHz = 400.0f;
    private const float GrainFilterHardnessRangeHz = 2200.0f;
    private const float GrainFilterQ = 0.8f;

    // A two-pole bandpass sheds only 6 dB/octave above its centre, and a Poisson impulse is
    // full-band to start with, so the band alone leaves nearly all the top end standing and
    // every surface arrives as hiss. The wind and friction models hit the same wall; the fix is
    // the same lowpass over the band, moving with hardness so brightness still tells stone from
    // snow instead of being a constant wash.
    private const float ToneCutoffBaseHz = 900.0f;
    private const float ToneCutoffHardnessRangeHz = 3500.0f;
    private const float ToneQ = 0.707f;
    private const float ModeDetuneRange = 0.06f;
    private const float ModeGainRollOffExponent = 0.3f;

    // At ResonanceDamping 1 this clamps to a couple of milliseconds, which is short enough that
    // the bank never gets to ring - the result is the dry scatter that leaves and twisted cord need.
    private const float ResonanceT60Base = 0.12f;
    private const float MinimumResonanceT60Seconds = 0.002f;

    // The body is levelled against the grains rather than left at whatever gain the bank
    // happens to produce. ResonatorBank scales each mode by (1 - r) to keep a long decay from
    // running away, which means a body left to ring comes out far quieter than a dead one - so
    // the damping knob was changing loudness far more than duration, which is the opposite of
    // what it is for. Levelling here leaves it changing only how long the body holds.
    private const float BodyLevelAgainstGrains = 0.8f;

    private const float WashCutoffBaseHz = 1200.0f;
    private const float WashCutoffHardnessRangeHz = 2000.0f;
    private const float WashQ = 0.707f;
    private const float WashMixScale = 0.8f;

    // Levelled by RMS and soft-limited, not merely peak-normalised. A Poisson grain stream has
    // an enormous crest factor - a measured 40 - so scaling its loudest spike to full scale puts
    // the texture 30 dB down and leaves a handful of grains poking out of silence. Grass came
    // back from the first listening pass as "a ticking clock", which is exactly that. Driving to
    // a target RMS and letting tanh round off whatever that pushes past full scale turns a row
    // of ticks into a surface, and the saturation it adds on the loudest grains is grit the
    // texture wants anyway.
    private const float TargetRms = 0.16f;
    private const float TargetPeak = 0.9f;
    private const float MinimumLengthSeconds = 0.05f;
    private const float RingTailMultiplier = 1.2f;

    // Irregular ratios so the body has a pitch rather than a hollow, evenly-spaced ring - the same
    // reasoning ImpactModel's mode table documents, at a much smaller scale here.
    private static readonly float[] ModeRatios = [1.0f, 1.7f, 2.6f, 3.9f];

    public static float[] Render(GranularSurface surface, GranularGesture gesture, int sampleRate, int seed)
    {
        var rng = new Rng(seed);

        var lengthSeconds = MathF.Max(
            gesture.AttackSeconds + (RingTailMultiplier * gesture.T60Seconds),
            MinimumLengthSeconds);
        var length = (int)(lengthSeconds * sampleRate);

        var grains = new float[length];
        var grainRate = surface.GrainsPerSecond * gesture.Intensity;
        var impulses = new PoissonImpulses(rng, sampleRate, grainRate, GrainMinimumAmplitude, GrainMaximumAmplitude);
        impulses.Fill(grains);

        var grainFilter = new Biquad(
            sampleRate,
            BiquadShape.BandPass,
            GrainFilterBaseHz + (GrainFilterHardnessRangeHz * surface.GrainHardness),
            GrainFilterQ);
        var filteredGrains = new float[length];
        for (var i = 0; i < length; i++)
        {
            filteredGrains[i] = grainFilter.Process(grains[i]);
        }

        var modes = BuildModes(surface, rng);
        var body = new float[length];
        new ResonatorBank(sampleRate, modes).Process(filteredGrains, body);
        LevelAgainst(body, filteredGrains);

        var wash = new float[length];
        var washFilter = new Biquad(
            sampleRate,
            BiquadShape.LowPass,
            WashCutoffBaseHz + (WashCutoffHardnessRangeHz * surface.GrainHardness),
            WashQ);
        var noise = new WhiteNoise(rng);
        var washGain = WashMixScale * surface.NoiseWash;
        for (var i = 0; i < length; i++)
        {
            wash[i] = washFilter.Process(noise.Next()) * washGain;
        }

        var tone = new Biquad(
            sampleRate,
            BiquadShape.LowPass,
            ToneCutoffBaseHz + (ToneCutoffHardnessRangeHz * surface.GrainHardness),
            ToneQ);

        var envelope = new Envelope(sampleRate, gesture.AttackSeconds, gesture.T60Seconds);
        var mixed = new float[length];
        for (var i = 0; i < length; i++)
        {
            var sample = tone.Process(filteredGrains[i] + body[i] + wash[i]);
            mixed[i] = sample * envelope.Next() * gesture.Intensity;
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

        // tanh already bounds the result; this only lands it on the same full-scale reference
        // every other model here uses, so nothing is judged louder for being differently scaled.
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

    private static void LevelAgainst(float[] body, float[] grains)
    {
        var bodyRms = Analysis.Rms(body);
        if (bodyRms <= 0.0f)
        {
            return;
        }

        var scale = BodyLevelAgainstGrains * Analysis.Rms(grains) / bodyRms;
        for (var i = 0; i < body.Length; i++)
        {
            body[i] *= scale;
        }
    }

    private static Mode[] BuildModes(GranularSurface surface, Rng rng)
    {
        // 1 - ResonanceDamping rather than a fixed value: a live ring and a dry scatter are the
        // same body at two settings of the same knob, not two code paths.
        var t60 = MathF.Max(ResonanceT60Base * (1.0f - surface.ResonanceDamping), MinimumResonanceT60Seconds);

        var modes = new Mode[ModeRatios.Length];
        for (var i = 0; i < ModeRatios.Length; i++)
        {
            var detune = 1.0f + ((rng.NextFloat() * ModeDetuneRange) - (ModeDetuneRange / 2.0f));
            var frequency = surface.ResonanceHz * ModeRatios[i] * detune;
            var gain = MathF.Pow(i + 1, -ModeGainRollOffExponent);

            modes[i] = new Mode(frequency, t60, gain);
        }

        return modes;
    }
}
