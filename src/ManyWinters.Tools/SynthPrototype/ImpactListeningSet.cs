using System.Globalization;
using System.Text.Json;
using ManyWinters.Audio;

namespace ManyWinters.Tools.SynthPrototype;

// Renders the impact model's phase 4 listening material: named files for tuning by ear, a
// concatenated sequence per pairing (repetition is where cheap synthesis shows), and a shuffled,
// unlabelled set for the user's blind material-naming gate.
public static class ImpactListeningSet
{
    private const int SampleRate = 22050;
    private const float Size = 1.0f;
    private const int SeedCount = 5;
    private const float SequenceOnsetSeconds = 0.3f;
    private const float ReportDecibelsBelowPeak = -40.0f;

    // Fixed so the blind set's letter-to-material mapping doesn't change between runs; nothing
    // about it is meant to be re-randomised.
    private const int ShuffleSeed = 20260926;

    private static readonly JsonSerializerOptions KeyJsonOptions = new() { WriteIndented = true };

    private static readonly ImpactMaterial Stone = new(Hardness: 1.0f, Toughness: 0.15f, Density: 2.0f);
    private static readonly ImpactMaterial Wood = new(Hardness: 0.4f, Toughness: 0.7f, Density: 0.5f);

    private static readonly Pairing[] Pairings =
    [
        new Pairing("stone-on-wood", Stone, Wood),
        new Pairing("stone-on-stone", Stone, Stone),
        new Pairing("wood-on-wood", Wood, Wood),
    ];

    public static IReadOnlyList<string> Render(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var blindDirectory = Path.Combine(outputDirectory, "blind");
        Directory.CreateDirectory(blindDirectory);

        var report = new List<string>();
        var entries = new List<Rendered>();

        foreach (var pairing in Pairings)
        {
            var renders = new float[SeedCount][];
            for (var seed = 1; seed <= SeedCount; seed++)
            {
                var samples = ImpactModel.Render(pairing.Striker, pairing.Struck, Size, SampleRate, seed);
                renders[seed - 1] = samples;
                entries.Add(new Rendered(pairing, seed, samples));

                var fileName = $"{pairing.Name}-{seed}.wav";
                WriteAndReport(Path.Combine(outputDirectory, fileName), fileName, samples, report);
            }

            var sequence = Concatenate(renders, SequenceOnsetSeconds);
            var sequenceFileName = $"{pairing.Name}-sequence.wav";
            WriteAndReport(Path.Combine(outputDirectory, sequenceFileName), sequenceFileName, sequence, report);
        }

        WriteBlindSet(outputDirectory, blindDirectory, entries);

        return report;
    }

    // Onsets 300 ms apart, samples added rather than replaced: a mode still ringing from the
    // previous hit is exactly what "the rhythm of work" sounds like, and is what a listener would
    // notice if the synthesis is too repetitive.
    private static float[] Concatenate(IReadOnlyList<float[]> renders, float onsetSeconds)
    {
        var onsetSamples = (int)(onsetSeconds * SampleRate);

        var length = 0;
        for (var i = 0; i < renders.Count; i++)
        {
            length = Math.Max(length, (i * onsetSamples) + renders[i].Length);
        }

        var output = new float[length];
        for (var i = 0; i < renders.Count; i++)
        {
            var offset = i * onsetSamples;
            var samples = renders[i];
            for (var j = 0; j < samples.Length; j++)
            {
                output[offset + j] += samples[j];
            }
        }

        return output;
    }

    // Fisher-Yates over Rng rather than System.Random: the blind set has to reshuffle the same
    // way on every machine, or the key written here would stop matching a re-render.
    //
    // Two things deliberately kept out of the listener's way. The key goes beside the blind
    // folder, not inside it, so opening the folder to play the files does not show the answers.
    // And no blind file gets a report line: its measurements are identical to those of the named
    // file it came from, so a reader of report.txt could match centroids and decode the whole set
    // without listening to anything.
    private static void WriteBlindSet(string outputDirectory, string blindDirectory, IReadOnlyList<Rendered> entries)
    {
        var order = Enumerable.Range(0, entries.Count).ToArray();
        var rng = new Rng(ShuffleSeed);
        for (var i = order.Length - 1; i > 0; i--)
        {
            var j = (int)(rng.NextFloat() * (i + 1));
            (order[i], order[j]) = (order[j], order[i]);
        }

        var key = new Dictionary<string, BlindKeyEntry>();
        for (var letterIndex = 0; letterIndex < order.Length; letterIndex++)
        {
            var entry = entries[order[letterIndex]];
            var name = ((char)('a' + letterIndex)).ToString();
            var fileName = $"{name}.wav";

            WavWriter.Write(Path.Combine(blindDirectory, fileName), entry.Samples, SampleRate);
            key[name] = new BlindKeyEntry(entry.Pairing.Name, entry.Seed);
        }

        var json = JsonSerializer.Serialize(key, KeyJsonOptions);
        File.WriteAllText(Path.Combine(outputDirectory, "blind-key.json"), json);
    }

    private static void WriteAndReport(string path, string label, float[] samples, List<string> report)
    {
        WavWriter.Write(path, samples, SampleRate);

        var rms = Analysis.Rms(samples);
        var peak = Analysis.Peak(samples);
        var centroid = Analysis.SpectralCentroid(samples, SampleRate);
        var duration = Analysis.DurationAbove(samples, SampleRate, ReportDecibelsBelowPeak);

        report.Add(string.Format(
            CultureInfo.InvariantCulture,
            "{0,-28} rms={1,6:F3} peak={2,6:F3} centroid={3,7:F0}Hz duration={4,6:F3}s",
            label,
            rms,
            peak,
            centroid,
            duration));
    }

    private readonly record struct Pairing(string Name, ImpactMaterial Striker, ImpactMaterial Struck);

    private readonly record struct Rendered(Pairing Pairing, int Seed, float[] Samples);

    // Serialised into blind-key.json and never read back in code - the user reads the file, after
    // grading the set by ear.
    // ReSharper disable NotAccessedPositionalProperty.Local
    private sealed record BlindKeyEntry(string Material, int Seed);
    // ReSharper restore NotAccessedPositionalProperty.Local
}
