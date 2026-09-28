namespace ManyWinters.Audio;

// A mutter driven by a Kelly-Lochbaum vocal tract (VocalTract) fed by a Liljencrants-Fant glottal
// pulse (Glottis) - one continuous simulation for the whole utterance, not a syllable rendered in
// isolation and stitched into a buffer, because a real throat does not reset between syllables
// either. What replaces the previous model's formant table is a small set of tongue shapes
// (TractShape) the tongue glides between one syllable at a time; VocalTract's own physics is what
// turns that glide into formants, and what makes a stop consonant a real silence followed by a
// physically released burst instead of a separately synthesised noise. Not speech - no phoneme
// sequencing, no lexicon - because unintelligible is the requirement, not a shortfall.
public static class VoiceModel
{
    private const float MovementMin = 0.6f;
    private const float MovementRange = 0.4f;

    // The tube's physical length is set by the rate it runs at, not by the caller's. Each of
    // VocalTract's 44 sections is one substep of delay, so a wave crosses it in 44 substeps and
    // the quarter-wave resonance lands at substepRate / 176 - which at upstream's own 44100 Hz
    // is 501 Hz, the textbook first formant of a neutral adult vowel. Running the tube at the
    // caller's 22050 instead made it twice as long: a 34 cm throat, and measurably an octave
    // low. So the reference is fixed here and the render is resampled to whatever was asked for.
    private const int ReferenceRate = 44100;

    // Tract length itself, as the one lever a real tract pulls on formant frequency: the same 44
    // sections run faster are a shorter tube. A child (Tract near 0) runs above the reference, a
    // big adult below it. Glottis is unaffected - its pitch is set in real seconds.
    private const float TractScaleBase = 0.75f;
    private const float TractScaleRange = 0.5f;

    // Same throat-vs-noise boundary the previous model's own pulse jitter needed: a sixth is
    // rough, a half is broken.
    private const float RoughnessJitterMin = 0.03f;
    private const float RoughnessJitterRange = 0.15f;

    // Tenseness is 1 - Breath at the floor (Glottis reads it as how buzzy versus aspirated the
    // fold closure is), and Energy pushes it higher still - a call closes the folds harder than a
    // mutter does, on top of whatever the identity's own breathiness already costs it.
    private const float TensenessEnergyBoost = 0.35f;

    // Most syllables, not half. Consonants and the silence in front of them are what the ear reads
    // as speech rather than as a run of voiced vowels.
    private const float ConsonantProbability = 0.8f;
    private const float ClosureMinSeconds = 0.025f;
    private const float ClosureRangeSeconds = 0.035f;

    // The glide occupies the front of the syllable, same as the previous model: a mouth heard
    // arriving at a vowel, not starting there.
    private const float GlideFraction = 0.4f;

    // A gentle fall across the whole utterance, in steps rather than swept.
    private const float Declination = 0.06f;
    private const float PitchStepJitterMin = 0.02f;
    private const float PitchStepJitterRange = 0.10f;

    private const float SlotJitter = 0.1f;

    // Retuned once per block, not per sample - the same trade a slowly retuned filter and the
    // previous model's formant glide both make: a tongue's own glide is slow enough that the
    // difference is inaudible, and stepping through the shape formula and the
    // reflection-coefficient recompute every sample is not free.
    private const int ControlBlock = 64;

    // A short linear ramp at both ends of the render, not a per-syllable envelope: VocalTract's
    // own physics now supplies the interior shaping (a real closure before a consonant, a real
    // released transient after it), so all that is left to guard against is the buffer's own cut
    // edges, which nothing internal to the simulation produces a clean fade for.
    private const float EdgeFadeSeconds = 0.01f;

    private const float TargetRms = 0.2f;
    private const float TargetPeak = 0.9f;

    // Five tongue shapes standing in for cardinal vowels - a place along the tract (TongueIndex),
    // how far it narrows there (TongueDiameter), and how open the lips are (LipDiameter). These
    // are chosen for a plausible spread across the tongue's front/back and open/close range, not
    // matched against any specific vowel table: unintelligible is the requirement, so what matters
    // is that five distinct, topologically sensible places exist for the tongue to travel between.
    private static readonly TractShape[] Vowels =
    [
        new(TongueIndex: 21.0f, TongueDiameter: 3.3f, LipDiameter: 1.5f), // open, central
        new(TongueIndex: 28.0f, TongueDiameter: 2.6f, LipDiameter: 1.4f), // front, mid
        new(TongueIndex: 30.0f, TongueDiameter: 1.8f, LipDiameter: 1.3f), // front, close
        new(TongueIndex: 18.0f, TongueDiameter: 2.3f, LipDiameter: 0.9f), // back, mid, rounded
        new(TongueIndex: 16.0f, TongueDiameter: 1.9f, LipDiameter: 0.7f), // back, close, rounded
    ];

    // Every entry above keeps the tract's rest-diameter formula comfortably clear of zero at its
    // own peak - a vowel that pinches shut by its own table entry would misfire the
    // release-transient logic used to tell a real stop's closure from a vowel passing through a
    // merely narrow shape on its way somewhere else.

    // The shape every vowel leans towards at low Energy, the same role NeutralFormants played in
    // the previous model and for the same reason: a mutter under the breath still distinguishes
    // its vowels, so the floor above (MovementMin) never lets Energy collapse them onto this. Not
    // an average of the table above (that blend lands on an oddly narrow shape of its own, no
    // vowel itself): Pink Trombone's own resting shape, upstream's choice of what a tract sits at
    // when nothing is asking it to do anything in particular.
    private static readonly TractShape NeutralShape = new(TongueIndex: 12.9f, TongueDiameter: 2.43f, LipDiameter: 1.5f);

    public static float[] Render(VoiceIdentity voice, Utterance utterance, int sampleRate, int seed)
    {
        var rng = new Rng(seed);

        var tractScale = TractScaleBase + (TractScaleRange * voice.Tract);
        var internalRate = Math.Max((int)MathF.Round(ReferenceRate / tractScale), 1000);

        var glottis = new Glottis(internalRate, rng);
        var tract = new VocalTract(internalRate);

        var plan = new SyllablePlan[utterance.SyllableCount];
        var previousShape = NeutralShape;
        var totalSamples = 0;

        for (var s = 0; s < utterance.SyllableCount; s++)
        {
            var slotSamples = (internalRate / utterance.Pace) * Jitter(rng, SlotJitter);

            var vowel = Vowels[(int)(rng.NextFloat() * Vowels.Length)];
            var movement = MovementMin + (MovementRange * utterance.Energy);
            var targetShape = BlendTowards(NeutralShape, vowel, movement);

            var declineProgress = utterance.SyllableCount > 1 ? s / (float)(utterance.SyllableCount - 1) : 0.0f;
            var pitchStep = PitchStepJitterMin + (PitchStepJitterRange * utterance.Energy);
            var pitchHz = voice.PitchHz * (1.0f - (Declination * declineProgress)) * Jitter(rng, pitchStep);

            var tenseness = Math.Clamp((1.0f - voice.Breath) + (TensenessEnergyBoost * utterance.Energy), 0.0f, 1.0f);
            var roughnessJitter = RoughnessJitterMin + (RoughnessJitterRange * voice.Roughness);

            var hasConsonant = rng.NextFloat() < ConsonantProbability;
            var fromShape = previousShape;
            var closureSamples = 0;

            if (hasConsonant)
            {
                closureSamples = Math.Max(
                    (int)((ClosureMinSeconds + (rng.NextFloat() * ClosureRangeSeconds)) * internalRate), 1);

                // Every closure is labial (lips shut) rather than a stop along the tongue's own
                // reach. A tongue closure was tried and measured stable on its own, but a tongue
                // shape whose TongueDiameter reaches zero right where VocalTract's own nose branch
                // couples in (its NoseStart, which two of the five vowel shapes above sit close
                // enough to) rings up a spuriously loud resonance there that swamps the rest of the
                // render once normalised - a real fragility in this port's nose-junction handling,
                // not a deliberately chosen restriction. Lips carry no such coupling at any vowel,
                // so every consonant closes there instead; the cost is one place of articulation
                // instead of two, which unintelligible babble does not need either of.
                fromShape = targetShape with { LipDiameter = 0.0f };
            }

            var contentSamples = Math.Max((int)slotSamples - closureSamples, 1);

            plan[s] = new SyllablePlan(fromShape, targetShape, pitchHz, tenseness, roughnessJitter, closureSamples, contentSamples);
            totalSamples += closureSamples + contentSamples;
            previousShape = targetShape;
        }

        var samples = new float[totalSamples];
        var cursor = 0;
        foreach (var syllable in plan)
        {
            glottis.SetSource(syllable.PitchHz, syllable.Tenseness, syllable.RoughnessJitter);

            // The closure: the tongue or lips already sit on the target shape's own consonant
            // place, so nothing further needs setting here beyond holding it - VocalTract reads a
            // zeroed diameter as silence on its own. SetShape is still called once per block, even
            // though the shape it passes never changes here: skipping it once the shape is settled
            // would leave Step interpolating forever between the block that first reached the
            // closure and the one before it, snapping back to the pre-closure reflection at the
            // start of every following block instead of staying put - a repeating discontinuity
            // that, against a closed cavity with nowhere to lose the energy it injects, was the
            // waveguide's route to blowing up.
            for (var i = 0; i < syllable.ClosureSamples; i++)
            {
                if (i % ControlBlock == 0)
                {
                    tract.SetShape(syllable.FromShape);
                }

                var blockProgress = (i % ControlBlock) / (float)ControlBlock;
                samples[cursor++] = tract.Step(glottis.Step(), blockProgress);
            }

            var glideSamples = Math.Max((int)(syllable.ContentSamples * GlideFraction), 1);
            for (var i = 0; i < syllable.ContentSamples; i++)
            {
                if (i % ControlBlock == 0)
                {
                    var progress = MathF.Min(i / (float)glideSamples, 1.0f);
                    tract.SetShape(BlendTowards(syllable.FromShape, syllable.TargetShape, progress));
                }

                var blockProgress = (i % ControlBlock) / (float)ControlBlock;
                samples[cursor++] = tract.Step(glottis.Step(), blockProgress);
            }
        }

        FadeEdges(samples, internalRate);

        return Normalise(Resample(samples, internalRate, sampleRate));
    }

    private static TractShape BlendTowards(TractShape from, TractShape to, float movement) => new(
        Lerp(from.TongueIndex, to.TongueIndex, movement),
        Lerp(from.TongueDiameter, to.TongueDiameter, movement),
        Lerp(from.LipDiameter, to.LipDiameter, movement));

    private static float Lerp(float from, float to, float p) => from + ((to - from) * p);

    private static float Jitter(Rng rng, float fraction) => 1.0f + ((rng.NextFloat() * 2.0f * fraction) - fraction);

    private static void FadeEdges(float[] samples, int sampleRate)
    {
        var fadeSamples = Math.Min((int)(EdgeFadeSeconds * sampleRate), samples.Length / 2);
        for (var i = 0; i < fadeSamples; i++)
        {
            var gain = i / (float)fadeSamples;
            samples[i] *= gain;
            samples[samples.Length - 1 - i] *= gain;
        }
    }

    // The one place VocalTract's own scale trick is turned back into the sample rate the caller
    // asked for. Linear interpolation rather than a proper band-limited resampler: the ratio is
    // always within TractScaleBase's own 0.75-1.25 range, and a stretch that gentle has no
    // meaningful aliasing for a signal already lowpassed by nothing sharper than a two-pole
    // waveguide junction.
    private static float[] Resample(float[] source, int fromRate, int toRate)
    {
        if (fromRate == toRate || source.Length == 0)
        {
            return source;
        }

        var length = Math.Max((int)MathF.Round(source.Length * (toRate / (float)fromRate)), 1);
        var result = new float[length];
        for (var i = 0; i < length; i++)
        {
            var position = i * (fromRate / (float)toRate);
            var index = (int)position;
            var fraction = position - index;
            var s0 = source[Math.Min(index, source.Length - 1)];
            var s1 = source[Math.Min(index + 1, source.Length - 1)];
            result[i] = s0 + ((s1 - s0) * fraction);
        }

        return result;
    }

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

    private readonly record struct SyllablePlan(
        TractShape FromShape,
        TractShape TargetShape,
        float PitchHz,
        float Tenseness,
        float RoughnessJitter,
        int ClosureSamples,
        int ContentSamples);
}
