using System.Globalization;
using ManyWinters.Audio;

namespace ManyWinters.Tools.SynthPrototype;

// Renders the friction model's tuning material: named files, five strokes each, so the numbers in
// FrictionModel's constants have something for the ear to check them against.
public static class FrictionListeningSet
{
    private const int SampleRate = 22050;
    private const int StrokeCount = 5;
    private const float ReportDecibelsBelowPeak = -40.0f;

    private static readonly (string FileName, FrictionStroke Stroke)[] Files =
    [
        ("sharpen-coarse.wav", new FrictionStroke(Grit: 0.20f, Pressure: 0.70f, StrokesPerSecond: 1.8f)),
        ("sharpen-fine.wav", new FrictionStroke(Grit: 0.80f, Pressure: 0.55f, StrokesPerSecond: 2.4f)),
        ("sharpen-heavy.wav", new FrictionStroke(Grit: 0.35f, Pressure: 1.00f, StrokesPerSecond: 1.3f)),
    ];

    public static IReadOnlyList<string> Render(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var report = new List<string>();
        foreach (var (fileName, stroke) in Files)
        {
            var samples = FrictionModel.Render(stroke, StrokeCount, SampleRate, seed: 1);
            WriteAndReport(Path.Combine(outputDirectory, fileName), fileName, samples, report);
        }

        return report;
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
}
