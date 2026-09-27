using System.Globalization;
using ManyWinters.Audio;

namespace ManyWinters.Tools.SynthPrototype;

// Renders the granular model's listening material: three seeds per surface for tuning by ear, an
// eight-step walk per surface (repetition is where a granular model's own texture shows, and the
// file that actually has to survive), and one file per gesture the surfaces are paired with.
public static class SurfaceListeningSet
{
    private const int SampleRate = 22050;
    private const int SeedCount = 3;
    private const int WalkStepCount = 8;
    private const float WalkOnsetSeconds = 0.55f;
    private const float WalkIntensityJitter = 0.15f;

    // Onsets exactly 0.55 s apart are a metronome, and grass came back from the first listening
    // pass as "a ticking clock" - which was the spacing, not the grain cloud. Nobody walks in
    // even intervals, and the trailing foot lands softer than the leading one, so the walk gets
    // both: a nudge on each interval and an alternating weight.
    private const float WalkOnsetJitter = 0.12f;
    private const float TrailingFootLevel = 0.82f;
    private const float ReportDecibelsBelowPeak = -40.0f;

    // Fixed so a re-render does not shuffle the walk against a previous listening pass.
    private const int WalkSeed = 20260927;

    // Second pass, against the ear. What each one was told: grass ticked like a clock, leaves
    // had too little crunch in them, water too little splash, snow did not squeak the way snow
    // squeaks, stone clinked.
    //
    // Grass is not a grain cloud at all but a swish, so its rate goes up and most of its sound
    // moves into the wash. Leaves go the other way - fewer grains, much harder, so the snaps are
    // individually audible, which is what crunch is. Stone's body was ringing at 1700 Hz, and a
    // ring at a fixed pitch is a clink; damping it kills the pitch and leaves the click. Snow's
    // squeak is a body that rings a little, so its damping comes down where stone's goes up.
    //
    // Third pass: stone, snow and leaves were called finished and are untouched; grass came back
    // a shade too bright and lost a little hardness and resonance.
    private static readonly Surface[] Surfaces =
    [
        new("grass", new GranularSurface(3000.0f, 0.06f, 600.0f, 0.92f, 0.55f)),
        new("soil", new GranularSurface(1200.0f, 0.22f, 300.0f, 0.88f, 0.35f)),
        new("stone", new GranularSurface(200.0f, 0.85f, 1700.0f, 0.85f, 0.05f)),
        new("snow", new GranularSurface(3500.0f, 0.30f, 900.0f, 0.55f, 0.45f)),
        new("leaves", new GranularSurface(700.0f, 0.55f, 1800.0f, 0.90f, 0.05f)),
    ];

    // Water is not in that table. Rounds of granular numbers were heard as banging on a gate
    // and then as still not water: a grain cloud has one envelope over one texture, where a
    // splash is three bands ending at three different times. It has its own model and its own
    // entry here.
    private static readonly LiquidSplash ShallowWater = new(Depth: 0.25f, Vigour: 0.7f);

    private static readonly GranularGesture Footstep = new(AttackSeconds: 0.006f, T60Seconds: 0.09f, Intensity: 0.8f);

    // Deep snow gives under the foot, which keeps moving after it lands.

    // Grass is brushed through rather than struck, so its footfall is a swish that needs longer
    // than a click to be one at all.
    private static readonly GranularGesture GrassStep = new(AttackSeconds: 0.012f, T60Seconds: 0.15f, Intensity: 0.75f);
    private static readonly GranularGesture SnowStep = new(AttackSeconds: 0.008f, T60Seconds: 0.13f, Intensity: 0.85f);
    private static readonly GranularGesture Dig = new(AttackSeconds: 0.050f, T60Seconds: 0.30f, Intensity: 1.0f);
    private static readonly GranularGesture Rummage = new(AttackSeconds: 0.030f, T60Seconds: 0.45f, Intensity: 0.6f);
    private static readonly GranularGesture TwistCord = new(AttackSeconds: 0.080f, T60Seconds: 0.55f, Intensity: 0.5f);
    // A long attack, because pulling is not striking: stems give way one after another, so the
    // sound builds and then tapers. At a 30 ms attack the loudest moment sat 7 % into the file
    // and it opened with a bang; at 90 ms the peak lands around a third of the way in.
    private static readonly GranularGesture GatherGrass = new(AttackSeconds: 0.090f, T60Seconds: 0.18f, Intensity: 0.8f);

    // A dry, sparse texture that shares no surface entry above: a rustle-and-scrape isolated from
    // any of the ground materials, to check the model reads as "cord" rather than "footstep".
    private static readonly GranularSurface TwistCordSurface = new(120.0f, 0.25f, 700.0f, 0.95f, 0.05f);

    // Pulling grass is not walking on it. The ground entry is a swish - a foot brushing past
    // blades - and gathering borrowed it, which is why it read as a slow-motion footstep. A
    // handful coming out of the ground is stems snapping: far fewer grains, far harder, and a
    // body damped almost flat, closer to the cord above than to the lawn underfoot.
    private static readonly GranularSurface GatherGrassSurface = new(450.0f, 0.45f, 1300.0f, 0.93f, 0.20f);

    public static IReadOnlyList<string> Render(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var report = new List<string>();

        foreach (var surface in Surfaces)
        {
            for (var seed = 1; seed <= SeedCount; seed++)
            {
                var samples = GranularModel.Render(surface.Parameters, StepFor(surface.Name), SampleRate, seed);
                var fileName = $"footstep-{surface.Name}-{seed}.wav";
                WriteAndReport(Path.Combine(outputDirectory, fileName), fileName, samples, report);
            }

            var walk = RenderWalk(surface.Parameters, StepFor(surface.Name));
            var walkFileName = $"footstep-{surface.Name}-walk.wav";
            WriteAndReport(Path.Combine(outputDirectory, walkFileName), walkFileName, walk, report);
        }

        for (var seed = 1; seed <= SeedCount; seed++)
        {
            var fileName = $"footstep-shallow-water-{seed}.wav";
            WriteAndReport(
                Path.Combine(outputDirectory, fileName),
                fileName,
                LiquidModel.Render(ShallowWater, SampleRate, seed),
                report);
        }

        WriteAndReport(
            Path.Combine(outputDirectory, "footstep-shallow-water-walk.wav"),
            "footstep-shallow-water-walk.wav",
            RenderWaterWalk(),
            report);

        RenderGesture("dig-soil.wav", Surfaces.Single(s => s.Name == "soil").Parameters, Dig, 1, report, outputDirectory);
        RenderGesture("rummage-leaves.wav", Surfaces.Single(s => s.Name == "leaves").Parameters, Rummage, 1, report, outputDirectory);
        RenderGesture("twist-cord.wav", TwistCordSurface, TwistCord, 1, report, outputDirectory);
        RenderGesture("gather-grass.wav", GatherGrassSurface, GatherGrass, 1, report, outputDirectory);

        return report;
    }

    // Onsets 550 ms apart, samples added rather than replaced, each step a different seed and its
    // intensity nudged from Rng so no two footfalls in the walk are identical - a fixed loop is
    // exactly what would give a granular model away as synthetic.
    // Snow and grass get their own footfall; everything else is the same gesture on a different
    // surface, which is the whole point of the split.
    private static GranularGesture StepFor(string surfaceName) => surfaceName switch
    {
        "snow" => SnowStep,
        "grass" => GrassStep,
        _ => Footstep,
    };

    // The same irregular gait as the granular walk, so the two are comparable by ear.
    private static float[] RenderWaterWalk()
    {
        var rng = new Rng(WalkSeed);

        var steps = new float[WalkStepCount][];
        var offsets = new int[WalkStepCount];
        var length = 0;
        var onset = 0;
        for (var i = 0; i < WalkStepCount; i++)
        {
            var weight = i % 2 == 0 ? 1.0f : TrailingFootLevel;
            var jitter = 1.0f + ((rng.NextFloat() * 2.0f * WalkIntensityJitter) - WalkIntensityJitter);
            var splash = ShallowWater with { Vigour = ShallowWater.Vigour * jitter * weight };
            var samples = LiquidModel.Render(splash, SampleRate, WalkSeed + i);

            steps[i] = samples;
            offsets[i] = onset;
            length = Math.Max(length, onset + samples.Length);

            var spacing = WalkOnsetSeconds * (1.0f + ((rng.NextFloat() * 2.0f * WalkOnsetJitter) - WalkOnsetJitter));
            onset += (int)(spacing * SampleRate);
        }

        var output = new float[length];
        for (var i = 0; i < WalkStepCount; i++)
        {
            var samples = steps[i];
            for (var j = 0; j < samples.Length; j++)
            {
                output[offsets[i] + j] += samples[j];
            }
        }

        return output;
    }

    private static float[] RenderWalk(GranularSurface surface, GranularGesture step)
    {
        var rng = new Rng(WalkSeed);

        var steps = new float[WalkStepCount][];
        var offsets = new int[WalkStepCount];
        var length = 0;
        var onset = 0;
        for (var i = 0; i < WalkStepCount; i++)
        {
            var weight = i % 2 == 0 ? 1.0f : TrailingFootLevel;
            var jitter = 1.0f + ((rng.NextFloat() * 2.0f * WalkIntensityJitter) - WalkIntensityJitter);
            var gesture = step with { Intensity = step.Intensity * jitter * weight };
            var samples = GranularModel.Render(surface, gesture, SampleRate, WalkSeed + i);

            steps[i] = samples;
            offsets[i] = onset;
            length = Math.Max(length, onset + samples.Length);

            var spacing = WalkOnsetSeconds * (1.0f + ((rng.NextFloat() * 2.0f * WalkOnsetJitter) - WalkOnsetJitter));
            onset += (int)(spacing * SampleRate);
        }

        var output = new float[length];
        for (var i = 0; i < WalkStepCount; i++)
        {
            var offset = offsets[i];
            var samples = steps[i];
            for (var j = 0; j < samples.Length; j++)
            {
                output[offset + j] += samples[j];
            }
        }

        return output;
    }

    private static void RenderGesture(
        string fileName, GranularSurface surface, GranularGesture gesture, int seed, List<string> report, string outputDirectory)
    {
        var samples = GranularModel.Render(surface, gesture, SampleRate, seed);
        WriteAndReport(Path.Combine(outputDirectory, fileName), fileName, samples, report);
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

    private readonly record struct Surface(string Name, GranularSurface Parameters);
}
