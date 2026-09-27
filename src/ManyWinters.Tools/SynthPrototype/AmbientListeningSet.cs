using System.Globalization;
using ManyWinters.Audio;

namespace ManyWinters.Tools.SynthPrototype;

// Each voice alone, then the four seasons as they would actually be heard. The voices come
// first because the songbird is the one sound in this prototype whose failure would be a
// matter of taste rather than physics, and a complaint about a whole season has to be
// traceable to a voice rather than to the mix.
public static class AmbientListeningSet
{
    private const int SampleRate = 22050;
    private const float ReportDecibelsBelowPeak = -40.0f;
    private const float SeasonSeconds = 20.0f;
    private const int ChunkSamples = 1024;

    // The mapping that will live in the game once Season reaches the presenter. Here only so the
    // four beds can be rendered; the library itself knows nothing about seasons.
    private static readonly (string Name, AmbientParameters Parameters)[] Seasons =
    [
        ("spring", new AmbientParameters(BirdDensity: 1.00f, InsectDensity: 0.25f, CorvidDensity: 0.10f, RustleDensity: 0.50f, Hush: 0.00f)),
        ("summer", new AmbientParameters(BirdDensity: 0.55f, InsectDensity: 1.00f, CorvidDensity: 0.10f, RustleDensity: 0.60f, Hush: 0.00f)),
        ("autumn", new AmbientParameters(BirdDensity: 0.30f, InsectDensity: 0.45f, CorvidDensity: 0.60f, RustleDensity: 0.70f, Hush: 0.15f)),
        ("winter", new AmbientParameters(BirdDensity: 0.05f, InsectDensity: 0.00f, CorvidDensity: 0.75f, RustleDensity: 0.25f, Hush: 1.00f)),
    ];

    // Two birds, three calls each: the point is not the phrase but whether the same seed comes
    // back as the same bird, because that recurrence is what the ambient layer is built on.
    private static readonly (string Name, BirdVoice Voice)[] Birds =
    [
        ("bird-high", new BirdVoice(PitchHz: 3200.0f, Brightness: 0.7f)),
        ("bird-low", new BirdVoice(PitchHz: 1700.0f, Brightness: 0.45f)),
    ];

    public static IReadOnlyList<string> Render(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var report = new List<string>();

        foreach (var (name, voice) in Birds)
        {
            for (var seed = 1; seed <= 3; seed++)
            {
                Write(outputDirectory, $"{name}-{seed}.wav", BirdCall.Render(voice, SampleRate, seed), report);
            }
        }

        Write(outputDirectory, "insect-cricket.wav", InsectTrill.Render(4600.0f, 0.9f, SampleRate, 5), report);
        Write(outputDirectory, "insect-cicada.wav", InsectTrill.Render(6200.0f, 1.4f, SampleRate, 6), report);
        Write(outputDirectory, "corvid-crow.wav", CorvidCaw.Render(0.4f, SampleRate, 7), report);
        Write(outputDirectory, "corvid-raven.wav", CorvidCaw.Render(0.9f, SampleRate, 8), report);

        foreach (var (name, parameters) in Seasons)
        {
            var source = new AmbientSource(SampleRate, seed: 20260927);
            source.Set(parameters);
            Write(
                outputDirectory,
                $"ambient-{name}.wav",
                SampleSourceRenderer.Render(source, SeasonSeconds, ChunkSamples),
                report);
        }

        return report;
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
