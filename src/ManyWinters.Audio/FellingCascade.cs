namespace ManyWinters.Audio;

// One scene built from the three impact/granular/creak models this library already has, laid out
// in time and mixed into a single buffer: chops, the hinge giving way, the fall, the crash. Every
// draw comes from one seeded Rng so the whole scene replays from a seed, the same guarantee each
// sub-model already gives its own render.
public static class FellingCascade
{
    private const float ChopSpacingSeconds = 0.85f;
    private const float ChopSpacingJitterFraction = 0.05f;

    // The last couple of chops land harder - the swing that finally opens the hinge - scaled up
    // before the final mix rather than given their own louder ImpactMaterial, so they stay the
    // same axe on the same wood.
    private const int LoudChopCount = 2;
    private const float LoudChopScale = 1.4f;

    private const float CreakLeadSeconds = 0.35f;
    private const float CreakDurationBaseSeconds = 0.8f;
    private const float CreakDurationSizeScaleSeconds = 1.2f;
    private const float CreakStrain = 0.8f;

    // The creak is tension building, not the payoff - its own render peaks the same as any other
    // model's, but held down here so its sustained swell under the crack does not out-loud the
    // crash that is meant to top the whole scene.
    private const float CreakMixScale = 0.4f;

    // A bigger tree breaks louder and lower, the same size relationship ImpactModel already gives
    // any pair - this just points a bigger multiplier at the hinge letting go.
    private const float CrackSizeScale = 2.5f;
    private const float CrackMixScale = 1.3f;

    // The hinge lets go while the creak is still sounding, not after it falls silent - so the
    // crack's onset sits inside the creak's own decaying tail rather than waiting for it to finish.
    private const float CrackOverlapSeconds = 0.15f;

    private static readonly GranularSurface FallSurface = new(
        GrainsPerSecond: 900.0f, GrainHardness: 0.5f, ResonanceHz: 1600.0f, ResonanceDamping: 0.9f, NoiseWash: 0.2f);
    private const float FallAttackSeconds = 0.25f;
    private const float FallT60BaseSeconds = 0.5f;
    private const float FallT60SizeScaleSeconds = 0.5f;
    private const float FallIntensity = 0.8f;

    // Leaves are already rustling by the time the trunk itself has finished cracking - the
    // branches are already moving through the air before the hinge fibre has fully parted. A
    // fraction of the crack's own length rather than a fixed offset: a fixed 0.2 s overlap is
    // longer than a small crack's whole decay and pulled the fall back to start alongside it
    // instead of near its tail, burying the crash under three simultaneous layers.
    private const float FallOverlapFraction = 0.3f;

    private const float CrashSizeScale = 4.0f;

    // The ground taking the whole trunk's weight is the loudest single event in the scene - louder
    // than the hinge letting go, which only has to carry a crack, not a landing.
    private const float CrashMixScale = 1.6f;
    private const int CrashDebrisMinimumCount = 6;
    private const int CrashDebrisMaximumCount = 10;
    private const float CrashDebrisWindowSeconds = 0.6f;

    // Branches breaking are a fraction of the trunk, never the trunk itself - scattering their
    // size in a band well under 1 is what keeps them reading as debris rather than a second crash.
    private const float CrashDebrisMinimumSizeFraction = 0.15f;
    private const float CrashDebrisMaximumSizeFraction = 0.4f;

    private static readonly GranularSurface CrashDebrisSurface = new(
        GrainsPerSecond: 600.0f, GrainHardness: 0.25f, ResonanceHz: 300.0f, ResonanceDamping: 0.9f, NoiseWash: 0.3f);
    private const float CrashDebrisGestureAttackSeconds = 0.05f;
    private const float CrashDebrisGestureT60Seconds = 0.35f;
    private const float CrashDebrisGestureIntensity = 1.0f;

    // Room after the last layer's own decay so nothing rendered here is clipped by the buffer edge.
    private const float TailSeconds = 0.3f;

    // The whole scene is sparse impacts over long stretches of near-silence, the spikiest crest
    // factor this library builds - driving to a target RMS and soft-limiting with tanh before the
    // final peak normalisation is the same fix GranularModel and LiquidModel needed for a single
    // grain stream or splash, only more so here.
    private const float TargetRms = 0.1f;
    private const float TargetPeak = 0.9f;

    public static float[] Render(FellingTree tree, int chopCount, int sampleRate, int seed)
    {
        var rng = new Rng(seed);
        var layers = new List<(float[] Samples, int OffsetSamples)>();

        var onsetSeconds = 0.0f;
        var chopEndSeconds = 0.0f;
        for (var i = 0; i < chopCount; i++)
        {
            var chop = ImpactModel.Render(tree.Tool, tree.Wood, tree.Size, sampleRate, NextSeed(rng));
            if (i >= chopCount - LoudChopCount)
            {
                Scale(chop, LoudChopScale);
            }

            layers.Add((chop, SecondsToSamples(onsetSeconds, sampleRate)));
            chopEndSeconds = MathF.Max(chopEndSeconds, onsetSeconds + (chop.Length / (float)sampleRate));

            var jitter = 1.0f + (((rng.NextFloat() * 2.0f) - 1.0f) * ChopSpacingJitterFraction);
            onsetSeconds += ChopSpacingSeconds * jitter;
        }

        var creakStart = chopEndSeconds + CreakLeadSeconds;
        var creakDuration = CreakDurationBaseSeconds + (CreakDurationSizeScaleSeconds * tree.Size);
        var creak = CreakModel.Render(new Creak(tree.Size, CreakStrain), creakDuration, sampleRate, NextSeed(rng));
        Scale(creak, CreakMixScale);
        layers.Add((creak, SecondsToSamples(creakStart, sampleRate)));
        var creakEnd = creakStart + (creak.Length / (float)sampleRate);

        var crackStart = MathF.Max(creakStart, creakEnd - CrackOverlapSeconds);
        var crack = ImpactModel.Render(tree.Wood, tree.Wood, tree.Size * CrackSizeScale, sampleRate, NextSeed(rng));
        Scale(crack, CrackMixScale);
        layers.Add((crack, SecondsToSamples(crackStart, sampleRate)));
        var crackEnd = crackStart + (crack.Length / (float)sampleRate);

        var fallStart = crackEnd - (FallOverlapFraction * (crackEnd - crackStart));
        var fallGesture = new GranularGesture(
            FallAttackSeconds, FallT60BaseSeconds + (FallT60SizeScaleSeconds * tree.Size), FallIntensity);
        var fall = GranularModel.Render(FallSurface, fallGesture, sampleRate, NextSeed(rng));
        layers.Add((fall, SecondsToSamples(fallStart, sampleRate)));
        var fallEnd = fallStart + (fall.Length / (float)sampleRate);

        var crashStart = fallEnd;
        var bigImpact = ImpactModel.Render(tree.Wood, tree.Wood, tree.Size * CrashSizeScale, sampleRate, NextSeed(rng));
        Scale(bigImpact, CrashMixScale);
        layers.Add((bigImpact, SecondsToSamples(crashStart, sampleRate)));

        var debrisCount = CrashDebrisMinimumCount
            + (int)(rng.NextFloat() * ((CrashDebrisMaximumCount - CrashDebrisMinimumCount) + 1));
        for (var i = 0; i < debrisCount; i++)
        {
            var debrisStart = crashStart + (rng.NextFloat() * CrashDebrisWindowSeconds);
            var debrisSizeFraction = CrashDebrisMinimumSizeFraction
                + (rng.NextFloat() * (CrashDebrisMaximumSizeFraction - CrashDebrisMinimumSizeFraction));
            var debris = ImpactModel.Render(
                tree.Wood, tree.Wood, tree.Size * debrisSizeFraction, sampleRate, NextSeed(rng));
            layers.Add((debris, SecondsToSamples(debrisStart, sampleRate)));
        }

        var debrisGesture = new GranularGesture(
            CrashDebrisGestureAttackSeconds, CrashDebrisGestureT60Seconds, CrashDebrisGestureIntensity);
        var soilBurst = GranularModel.Render(CrashDebrisSurface, debrisGesture, sampleRate, NextSeed(rng));
        layers.Add((soilBurst, SecondsToSamples(crashStart, sampleRate)));

        var length = 0;
        foreach (var (samples, offset) in layers)
        {
            length = Math.Max(length, offset + samples.Length);
        }

        length += (int)(TailSeconds * sampleRate);

        var mixed = new float[length];
        foreach (var (samples, offset) in layers)
        {
            for (var i = 0; i < samples.Length; i++)
            {
                mixed[offset + i] += samples[i];
            }
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

    private static int SecondsToSamples(float seconds, int sampleRate) => (int)(seconds * sampleRate);

    private static void Scale(float[] samples, float scale)
    {
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] *= scale;
        }
    }

    // The one place this scene draws a seed for a sub-model rather than a parameter: every render
    // still traces back to the single Rng the whole cascade was constructed from.
    private static int NextSeed(Rng rng) => (int)(rng.NextFloat() * int.MaxValue);
}
