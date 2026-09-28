namespace ManyWinters.Audio;

// A phrase, not a note: two to five short notes separated by silence, each a glissando rather
// than a fixed pitch. This is the flute exception audio-architecture.md section 5.3 carves out
// of "synthesis fails at animal voices" - every choice here favours a clean gliding tone over
// anything that chases a real songbird's throat, right down to a wooden instrument's soft attack.
public static class BirdCall
{
    private const int MinimumNotes = 2;
    private const int NoteCountRange = 4; // draws 0..3, so 2..5 notes total

    private const float NoteMinSeconds = 0.04f;
    private const float NoteRangeSeconds = 0.09f;
    private const float GapMinSeconds = 0.02f;
    private const float GapRangeSeconds = 0.06f;

    // The chirp is the glide, not the pitch: how far a note moves is what Brightness scales.
    private const float GlideFractionMin = 0.03f;
    private const float GlideFractionRange = 0.22f;
    private const float GlideJitter = 0.3f;

    // Soft, not Envelope's usual percussive attack: a flute note is blown into, not struck.
    private const float AttackSeconds = 0.008f;

    private const float HarmonicGain = 0.25f;

    // A little breath, gated by the same envelope as the tone, so the call has grain instead of
    // being a bare test tone - not a throat, just enough air to say "this was blown".
    private const float BreathGain = 0.06f;
    private const float BreathHighpassHz = 3000.0f;
    private const float BreathQ = 0.7f;

    private const float TargetRms = 0.2f;
    private const float TargetPeak = 0.9f;

    // A small fixed pentatonic-ish set of ratios against the voice's own pitch, rather than a
    // free choice of interval: a flute phrase moves between a handful of related notes, not
    // continuously through the scale.
    private static readonly float[] RatioSet = [1.0f, 1.125f, 1.25f, 1.5f, 1.667f, 2.0f];

    public static float[] Render(BirdVoice voice, int sampleRate, int seed)
    {
        var rng = new Rng(seed);
        var noteCount = MinimumNotes + (int)(rng.NextFloat() * NoteCountRange);

        var notes = new float[noteCount][];
        for (var n = 0; n < noteCount; n++)
        {
            notes[n] = RenderNote(voice, sampleRate, rng);
        }

        var gapSamples = new int[noteCount];
        var length = 0;
        for (var n = 0; n < noteCount; n++)
        {
            length += notes[n].Length;
            if (n < noteCount - 1)
            {
                gapSamples[n] = (int)((GapMinSeconds + (rng.NextFloat() * GapRangeSeconds)) * sampleRate);
                length += gapSamples[n];
            }
        }

        var mixed = new float[length];
        var cursor = 0;
        for (var n = 0; n < noteCount; n++)
        {
            notes[n].CopyTo(mixed, cursor);
            cursor += notes[n].Length + gapSamples[n];
        }

        return Normalise(mixed);
    }

    private static float[] RenderNote(BirdVoice voice, int sampleRate, Rng rng)
    {
        var noteSeconds = NoteMinSeconds + (rng.NextFloat() * NoteRangeSeconds);
        var length = Math.Max((int)(noteSeconds * sampleRate), 1);

        var ratio = RatioSet[(int)(rng.NextFloat() * RatioSet.Length)];
        var startHz = voice.PitchHz * ratio;

        var glideFraction = (GlideFractionMin + (GlideFractionRange * voice.Brightness)) * Jitter(rng, GlideJitter);
        var direction = rng.NextFloat() < 0.5f ? -1.0f : 1.0f;
        var endHz = startHz * (1.0f + (direction * glideFraction));

        var fundamental = new Oscillator(sampleRate, Waveform.Sine, startHz);
        fundamental.GlideTo(endHz, noteSeconds);

        // Not a bare sine: a second harmonic is what tells the ear "wooden instrument" rather
        // than "sine wave generator".
        var harmonic = new Oscillator(sampleRate, Waveform.Sine, startHz * 2.0f);
        harmonic.GlideTo(endHz * 2.0f, noteSeconds);

        var noise = new WhiteNoise(rng);
        var breathFilter = new Biquad(sampleRate, BiquadShape.HighPass, BreathHighpassHz, BreathQ);
        var envelope = new Envelope(sampleRate, AttackSeconds, noteSeconds / 3.0f);

        var note = new float[length];
        for (var i = 0; i < length; i++)
        {
            var breath = breathFilter.Process(noise.Next());
            var tone = fundamental.Next() + (HarmonicGain * harmonic.Next()) + (BreathGain * breath);
            note[i] = tone * envelope.Next();
        }

        return note;
    }

    private static float Jitter(Rng rng, float fraction) => 1.0f + ((rng.NextFloat() * 2.0f * fraction) - fraction);

    // A run of short notes is spiky against its own gaps, so peak normalisation alone would leave
    // it too quiet - the same levelling every impulse-built model here needs.
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
