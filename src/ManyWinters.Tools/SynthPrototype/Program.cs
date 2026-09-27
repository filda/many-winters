using ManyWinters.Tools.SynthPrototype;

// Renders the audio prototype's listening set to disk. Run it through the Cake target
// (`--target=RenderAudio`), which passes the git-ignored artifacts folder as the output.
var outputDirectory = "artifacts/audio";
if (args.Length >= 2 && args[0] == "--out")
{
    outputDirectory = args[1];
}

Directory.CreateDirectory(outputDirectory);

// One report file for both sets: the numbers are what the ear is checked against during tuning,
// and having them side by side is the point of collecting them here rather than in each renderer.
List<string> report =
[
    .. ImpactListeningSet.Render(outputDirectory),
    .. SurfaceListeningSet.Render(outputDirectory),
    .. FrictionListeningSet.Render(outputDirectory),
    .. FellingListeningSet.Render(outputDirectory),
    .. AmbientListeningSet.Render(outputDirectory),
    .. VoiceListeningSet.Render(outputDirectory),
    .. WindSweep.Render(outputDirectory),
];
File.WriteAllLines(Path.Combine(outputDirectory, "report.txt"), report);

Console.WriteLine($"Rendered into {Path.GetFullPath(outputDirectory)}; {report.Count} lines in report.txt");
Console.WriteLine("Blind listening set: blind/a.wav .. blind/o.wav, answers in blind-key.json");
