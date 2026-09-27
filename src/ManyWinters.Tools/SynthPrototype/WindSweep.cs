using System.Globalization;
using ManyWinters.Audio;

namespace ManyWinters.Tools.SynthPrototype;

// Twenty seconds sweeping calm to storm and back to calm, rendered through the same
// ISampleSource path the game will pull from - a WindSource wrapped so it can drive its own
// parameters over time.
public static class WindSweep
{
    private const int OutputSampleRate = 22050;
    private const float DurationSeconds = 20.0f;
    private const float HoldSeconds = 8.0f;

    public static IReadOnlyList<string> Render(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var source = new Sweep(new WindSource(OutputSampleRate, seed: 20260926));
        var samples = SampleSourceRenderer.Render(source, DurationSeconds, chunkSamples: 1024);

        var path = Path.Combine(outputDirectory, "wind-sweep.wav");
        WavWriter.Write(path, samples, OutputSampleRate);

        var rms = Analysis.Rms(samples);
        var peak = Analysis.Peak(samples);
        var centroid = Analysis.SpectralCentroid(samples, OutputSampleRate);

        // Same column layout as the impact lines: they share one report.txt, and a reader
        // scanning the centroid column should not have to find it twice.
        return
        [
            string.Format(
                CultureInfo.InvariantCulture,
                "{0,-28} rms={1,6:F3} peak={2,6:F3} centroid={3,7:F0}Hz",
                "wind-sweep.wav",
                rms,
                peak,
                centroid),
            // The sweep passes through every strength, which makes it hard to judge any one of
            // them. These two hold still at the ends the ear actually has to accept.
            Hold(outputDirectory, "wind-breeze.wav", new WindParameters(Strength: 0.15f, Gustiness: 0.2f), seed: 41),
            Hold(outputDirectory, "wind-gale.wav", new WindParameters(Strength: 0.9f, Gustiness: 0.8f), seed: 42),
        ];
    }

    private static string Hold(string outputDirectory, string fileName, WindParameters parameters, int seed)
    {
        var wind = new WindSource(OutputSampleRate, seed);
        wind.Set(parameters);
        var samples = SampleSourceRenderer.Render(wind, HoldSeconds, chunkSamples: 1024);

        WavWriter.Write(Path.Combine(outputDirectory, fileName), samples, OutputSampleRate);

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0,-28} rms={1,6:F3} peak={2,6:F3} centroid={3,7:F0}Hz",
            fileName,
            Analysis.Rms(samples),
            Analysis.Peak(samples),
            Analysis.SpectralCentroid(samples, OutputSampleRate));
    }

    // Calm -> storm -> calm. Squared, not a plain triangle: a linear ramp spends as long in the
    // gale as in the breeze, and the breeze is the part that has to be judged.
    private sealed class Sweep(WindSource wind) : ISampleSource
    {
        private int _samplesRendered;

        public int SampleRate => wind.SampleRate;

        public void Read(Span<float> buffer)
        {
            var progress = _samplesRendered / (DurationSeconds * wind.SampleRate);
            var triangle = 1.0f - MathF.Abs((2.0f * progress) - 1.0f);
            var shaped = triangle * triangle;

            wind.Set(new WindParameters(Strength: shaped, Gustiness: shaped));
            wind.Read(buffer);

            _samplesRendered += buffer.Length;
        }
    }
}
