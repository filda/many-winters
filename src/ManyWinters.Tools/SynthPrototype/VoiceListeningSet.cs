using System.Globalization;
using ManyWinters.Audio;

namespace ManyWinters.Tools.SynthPrototype;

// The one question phase 3's voices left open: does a person's voice fit here at all, or is a
// person the class that keeps recorded samples (audio-architecture.md section 5.3). Four people
// answer whether the register reads as low and indistinct rather than as Animal Crossing's
// squeaky chatter; the repeated-seed set and the conversation answer whether an identity actually
// holds while what is said changes and while two voices sit side by side.
public static class VoiceListeningSet
{
    private const int SampleRate = 22050;
    private const float ReportDecibelsBelowPeak = -40.0f;
    private const float GapSeconds = 0.5f;
    private const float ConversationTargetSeconds = 10.0f;

    // Slower and shorter than the village-animal register the first casting landed on. Fewer
    // syllables with more air around them reads as people who are not chattering.
    private static readonly Utterance Mutter = new(SyllableCount: 3, Pace: 2.1f, Energy: 0.2f);
    private static readonly Utterance Call = new(SyllableCount: 2, Pace: 1.5f, Energy: 0.9f);

    // Second casting. The model was right and the register was borrowed: high, fast and clean is
    // Animal Crossing, which is what the first set was heard as. A band living outdoors is lower,
    // longer in the tract, and its voices are worn - so every pitch drops by roughly a fifth and
    // roughness and breath rise across the board. Nothing in the model changed for this; the
    // whole difference is these numbers, which is the point of a parametric voice.
    //
    // Pitch spans the range VoiceIdentity documents (a big man to a child); Tract, Roughness and
    // Breath each move with age in the same direction a real voice's do, so the four read as a
    // family rather than four unrelated dials.
    private static readonly (string Name, VoiceIdentity Voice)[] People =
    [
        ("child", new VoiceIdentity(PitchHz: 190.0f, Tract: 0.30f, Roughness: 0.18f, Breath: 0.45f)),
        ("woman", new VoiceIdentity(PitchHz: 155.0f, Tract: 0.55f, Roughness: 0.32f, Breath: 0.45f)),
        ("man", new VoiceIdentity(PitchHz: 86.0f, Tract: 0.82f, Roughness: 0.45f, Breath: 0.55f)),
        ("old-man", new VoiceIdentity(PitchHz: 70.0f, Tract: 0.88f, Roughness: 0.72f, Breath: 0.70f)),
    ];

    public static IReadOnlyList<string> Render(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var report = new List<string>();

        foreach (var (name, voice) in People)
        {
            Write(outputDirectory, $"{name}-mutter.wav", VoiceModel.Render(voice, Mutter, SampleRate, 1), report);
            Write(outputDirectory, $"{name}-call.wav", VoiceModel.Render(voice, Call, SampleRate, 2), report);
        }

        // The file that decides whether identity works: the same person, the same mutter shape,
        // three different seeds, so only the phrase changes and the voice itself can be judged on
        // whether it stays recognisable across the three.
        var (identityName, identityVoice) = People[1];
        for (var seed = 101; seed <= 103; seed++)
        {
            Write(
                outputDirectory,
                $"{identityName}-mutter-seed{seed - 100}.wav",
                VoiceModel.Render(identityVoice, Mutter, SampleRate, seed),
                report);
        }

        Write(outputDirectory, "conversation.wav", RenderConversation(), report);

        return report;
    }

    // Two voices side by side is the only way to hear whether they are actually distinguishable
    // rather than merely different on paper.
    private static float[] RenderConversation()
    {
        var speakers = new[] { People[1], People[2] };
        var gapSamples = (int)(GapSeconds * SampleRate);

        var lines = new List<float[]>();
        var totalSamples = 0;
        var turn = 0;
        var seed = 201;
        while (totalSamples < ConversationTargetSeconds * SampleRate)
        {
            var (_, voice) = speakers[turn % speakers.Length];
            var utterance = Mutter with { Energy = 0.35f };
            var line = VoiceModel.Render(voice, utterance, SampleRate, seed);

            lines.Add(line);
            totalSamples += line.Length + gapSamples;
            turn++;
            seed++;
        }

        var mixed = new float[totalSamples];
        var cursor = 0;
        foreach (var line in lines)
        {
            line.CopyTo(mixed, cursor);
            cursor += line.Length + gapSamples;
        }

        return mixed;
    }

    private static void Write(string outputDirectory, string fileName, float[] samples, List<string> report)
    {
        WavWriter.Write(Path.Combine(outputDirectory, fileName), samples, SampleRate);

        report.Add(string.Format(
            CultureInfo.InvariantCulture,
            "{0,-28} rms={1,6:F3} peak={2,6:F3} centroid={3,7:F0}Hz duration={4,6:F3}s",
            fileName,
            Analysis.Rms(samples),
            Analysis.Peak(samples),
            Analysis.SpectralCentroid(samples, SampleRate),
            Analysis.DurationAbove(samples, SampleRate, ReportDecibelsBelowPeak)));
    }
}
