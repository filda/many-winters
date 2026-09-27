using System.Globalization;
using ManyWinters.Audio;

namespace ManyWinters.Tools.SynthPrototype;

// The felling scene, and the creak on its own. The creak is separated deliberately: it is the
// one new model here, everything else in the cascade is a model the ear has already passed, so
// a complaint about the whole scene has to be traceable to either the creak or the balance
// between the parts rather than to both at once.
public static class FellingListeningSet
{
    private const int SampleRate = 22050;
    private const float ReportDecibelsBelowPeak = -40.0f;
    private const int ChopCount = 5;

    private static readonly ImpactMaterial Stone = new(Hardness: 1.0f, Toughness: 0.15f, Density: 2.0f);
    private static readonly ImpactMaterial Wood = new(Hardness: 0.4f, Toughness: 0.7f, Density: 0.5f);

    private static readonly (string Name, float Size)[] Trees =
    [
        ("sapling", 0.3f),
        ("tree", 0.7f),
        ("old-tree", 1.2f),
    ];

    // Held at one Size so the two strains are comparable: early in the cut against about to go.
    private static readonly (string Name, Creak Parameters, float Seconds)[] Creaks =
    [
        ("creak-early", new Creak(Size: 0.7f, Strain: 0.25f), 1.6f),
        ("creak-giving", new Creak(Size: 0.7f, Strain: 0.9f), 1.6f),
        ("creak-old-tree", new Creak(Size: 1.2f, Strain: 0.9f), 2.2f),
    ];

    public static IReadOnlyList<string> Render(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var report = new List<string>();

        foreach (var (name, parameters, seconds) in Creaks)
        {
            var fileName = $"{name}.wav";
            WriteAndReport(
                Path.Combine(outputDirectory, fileName),
                fileName,
                CreakModel.Render(parameters, seconds, SampleRate, seed: 31),
                report);
        }

        foreach (var (name, size) in Trees)
        {
            // Two seeds each: the scene is long enough that one render says little about whether
            // a second felling would sound like a repeat of the first.
            for (var seed = 1; seed <= 2; seed++)
            {
                var fileName = $"felling-{name}-{seed}.wav";
                WriteAndReport(
                    Path.Combine(outputDirectory, fileName),
                    fileName,
                    FellingCascade.Render(new FellingTree(size, Wood, Stone), ChopCount, SampleRate, seed),
                    report);
            }
        }

        return report;
    }

    private static void WriteAndReport(string path, string label, float[] samples, List<string> report)
    {
        WavWriter.Write(path, samples, SampleRate);

        report.Add(string.Format(
            CultureInfo.InvariantCulture,
            "{0,-28} rms={1,6:F3} peak={2,6:F3} centroid={3,7:F0}Hz duration={4,6:F3}s",
            label,
            Analysis.Rms(samples),
            Analysis.Peak(samples),
            Analysis.SpectralCentroid(samples, SampleRate),
            Analysis.DurationAbove(samples, SampleRate, ReportDecibelsBelowPeak)));
    }
}
