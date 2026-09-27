namespace ManyWinters.Audio;

// A cricket-like trill: a sine carrier amplitude-modulated by a fast, hard-edged stridulation
// rate rather than a smooth tremolo - the modulator is a sawtooth pushed through an exponent so
// its rising edge sharpens into the buzzy on/off of a wing stroke, not a wobble. The whole trill
// swells in across the render and fades rather than switching on, and the carrier's own pitch
// wobbles slightly so a held trill is not a test tone.
public static class InsectTrill
{
    private const float StridulationRateMinHz = 25.0f;
    private const float StridulationRateRangeHz = 35.0f;
    private const float StridulationSharpness = 4.0f;

    // A control-block pitch wobble, at the block rate WindSource retunes its filters at: a
    // bounded random walk sampled once per block and glided to smoothly, rather than a step
    // every sample.
    private const int ControlBlock = 64;
    private const float WobbleDepthFraction = 0.015f;
    private const float WobbleRateHz = 3.0f;

    private const float SwellRiseFraction = 0.2f;
    private const float SwellHoldFraction = 0.8f;

    private const float TargetRms = 0.22f;
    private const float TargetPeak = 0.9f;

    public static float[] Render(float pitchHz, float seconds, int sampleRate, int seed)
    {
        var rng = new Rng(seed);
        var length = Math.Max((int)(seconds * sampleRate), 1);

        var carrier = new Oscillator(sampleRate, Waveform.Sine, pitchHz);

        var blockRate = sampleRate / (float)ControlBlock;
        var wobbleSampleRate = Math.Max(1, (int)MathF.Round(blockRate));
        var wobble = new RandomWalkLfo(rng, wobbleSampleRate, WobbleRateHz, -WobbleDepthFraction, WobbleDepthFraction);

        var stridulationHz = StridulationRateMinHz + (rng.NextFloat() * StridulationRateRangeHz);
        var modulator = new Oscillator(sampleRate, Waveform.Sawtooth, stridulationHz);

        var samples = new float[length];
        var samplesUntilWobble = 0;
        for (var i = 0; i < length; i++)
        {
            if (samplesUntilWobble == 0)
            {
                var target = pitchHz * (1.0f + wobble.Next());
                carrier.GlideTo(target, ControlBlock / (float)sampleRate);
                samplesUntilWobble = ControlBlock;
            }

            samplesUntilWobble--;

            // (saw+1)/2 gives 0..1, and raising that to a power crowds the shape towards the
            // edges rather than a smooth mid-swing tremolo - the rasp instead of a wobble.
            var raw = (modulator.Next() + 1.0f) / 2.0f;
            var rasp = MathF.Pow(raw, StridulationSharpness);

            var p = length > 1 ? i / (float)(length - 1) : 1.0f;
            samples[i] = carrier.Next() * rasp * SwellEnvelope(p);
        }

        return Normalise(samples);
    }

    // Rises across the first fifth, holds, then eases off - the same raised-cosine swell
    // FrictionModel and CreakModel use, stretched over the whole trill.
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
