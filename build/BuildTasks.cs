using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace ManyWinters.Build;
[TaskName("LineEndings")]
public sealed class LineEndingsTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        // .gitattributes normalises the index, so a CRLF file in the working tree is invisible to
        // git status: the repository holds LF while every tool reading the file sees CRLF.
        var offenders = BuildProcess.Capture(context, "git", "ls-files", "--eol")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains(" w/crlf", StringComparison.Ordinal) || line.Contains(" w/mixed", StringComparison.Ordinal))
            .Select(line => line[(line.IndexOf('\t', StringComparison.Ordinal) + 1)..].TrimEnd('\r'))
            .ToList();

        if (offenders.Count != 0)
        {
            throw new InvalidOperationException(
                "Tracked files with CRLF or mixed line endings in the working tree; rewrite them with LF:"
                + Environment.NewLine + string.Join(Environment.NewLine, offenders));
        }
    }
}

[TaskName("FormatJson")]
public sealed class FormatJsonTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        foreach (var path in ContentJson.Files(context))
        {
            var original = File.ReadAllText(path);
            var formatted = ContentJson.Format(original);
            if (formatted == original)
            {
                continue;
            }

            File.WriteAllText(path, formatted);
            context.Log.Information(Verbosity.Normal, "Reformatted {0}", Path.GetRelativePath(context.RootDirectory, path));
        }
    }
}

[TaskName("FormatJsonCheck")]
public sealed class FormatJsonCheckTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var offenders = ContentJson.Misformatted(context);
        if (offenders.Count != 0)
        {
            throw new InvalidOperationException(
                "Content JSON files not in the canonical form; run --target=FormatJson:"
                + Environment.NewLine + string.Join(Environment.NewLine, offenders));
        }
    }
}

[TaskName("TextureMipmaps")]
public sealed class TextureMipmapsTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var offenders = TextureImports.WithoutMipmaps(context);
        if (offenders.Count != 0)
        {
            throw new InvalidOperationException(
                "Textures imported without mipmaps; set mipmaps/generate=true in their .import files and reimport:"
                + Environment.NewLine + string.Join(Environment.NewLine, offenders));
        }
    }
}

[TaskName("Restore")]
public sealed class RestoreTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        BuildProcess.Run(context, "dotnet", "restore", context.SolutionPath);
        BuildProcess.Run(context, "dotnet", "tool", "restore");
    }
}

[TaskName("Format")]
[IsDependentOn(typeof(RestoreTask))]
public sealed class FormatTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        BuildProcess.Run(context, "dotnet", "format", context.SolutionPath, "--verify-no-changes", "--severity", "warn");
        BuildProcess.Run(context, "dotnet", "format", context.BuildProjectPath, "--verify-no-changes", "--severity", "warn");
    }
}

[TaskName("Build")]
[IsDependentOn(typeof(RestoreTask))]
public sealed class BuildTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        BuildProcess.Run(context, "dotnet", "build", context.SolutionPath, "--configuration", context.BuildConfiguration);
    }
}

[TaskName("Test")]
[IsDependentOn(typeof(BuildTask))]
public sealed class TestTask : FrostingTask<BuildContext>
{
    // Each project runs as the xunit v3 executable it is, not through `dotnet test`: only the
    // native runner has the quiet reporter, which prints the failures and nothing else. A failing
    // project does not stop the next one from running, so one run reports every failure.
    //
    // The tests run in a random order (RandomTestOrder), and the quiet reporter does not print the
    // seed it drew, so the seed is drawn here and printed instead: `--seed=N` replays that order.
    public override void Run(BuildContext context)
    {
        var seed = context.TestOrderSeed ?? Random.Shared.Next().ToString(CultureInfo.InvariantCulture);
        context.Log.Information(Verbosity.Normal, "Test order seed: {0} (replay with --seed={0})", seed);

        var failed = new List<string>();
        foreach (var project in context.UnitTestProjectPaths)
        {
            try
            {
                BuildProcess.Run(context, "dotnet", "run", "--project", project, "--no-build", "--configuration", context.BuildConfiguration, "--", ":" + seed, "-reporter", "quiet");
            }
            catch (InvalidOperationException)
            {
                failed.Add(Path.GetFileNameWithoutExtension(project));
            }
        }

        if (failed.Count > 0)
        {
            throw new InvalidOperationException("Tests failed in " + string.Join(", ", failed) + ".");
        }
    }
}

[TaskName("InspectCode")]
[IsDependentOn(typeof(BuildTask))]
public sealed class InspectCodeTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        Directory.CreateDirectory(context.ArtifactsDirectory);
        var report = Path.Combine(context.ArtifactsDirectory, "inspectcode.xml");
        var arguments = new List<string>
        {
            "jb", "inspectcode", context.SolutionPath, "--swea", "--no-build", "--severity=WARNING",
            "--properties:Configuration=" + context.BuildConfiguration, "-f=Xml", "-o=" + report,
            "--caches-home=" + context.InspectCodeCacheDirectory,
        };

        if (OperatingSystem.IsWindows())
        {
            // Without this InspectCode may pick a Visual Studio Build Tools MSBuild and abort with
            // "the IDE failed to connect to it"; the SDK's own MSBuild works.
            arguments.Add("--toolset-path=" + BuildProcess.FindMsBuildPath(context));
        }

        BuildProcess.Run(context, "dotnet", [.. arguments]);

        var issueCount = File.ReadLines(report).Count(line => line.Contains("<Issue ", StringComparison.Ordinal));
        if (issueCount != 0)
        {
            throw new InvalidOperationException($"InspectCode reported {issueCount} issue(s). See {report}.");
        }
    }
}

// Deliberately not a dependency of InspectCode: a warm cache is what keeps that target at
// seconds instead of a minute. Run this first when InspectCode reports CSharpErrors on code
// that builds - the cache has fallen out of step with the sources, not the other way round.
[TaskName("InspectCodeClean")]
public sealed class InspectCodeCleanTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        if (Directory.Exists(context.InspectCodeCacheDirectory))
        {
            Directory.Delete(context.InspectCodeCacheDirectory, recursive: true);
        }
    }
}

[TaskName("RenderAudio")]
public sealed class RenderAudioTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        // Deliberately outside the CI gate: it produces files for a human to listen to, and the
        // listening is the check. Under artifacts/ so git ignores the WAVs.
        var output = Path.Combine(context.ArtifactsDirectory, "audio");
        BuildProcess.Run(context, "dotnet", "run", "--project", context.SynthPrototypeProjectPath, "--configuration", context.BuildConfiguration, "--", "--out", output);
    }
}

[TaskName("CI")]
[IsDependentOn(typeof(LineEndingsTask))]
[IsDependentOn(typeof(FormatJsonCheckTask))]
[IsDependentOn(typeof(TextureMipmapsTask))]
[IsDependentOn(typeof(RestoreTask))]
[IsDependentOn(typeof(FormatTask))]
[IsDependentOn(typeof(BuildTask))]
[IsDependentOn(typeof(InspectCodeTask))]
[IsDependentOn(typeof(TestTask))]
[IsDependentOn(typeof(E2ETask))]
public sealed class CiTask : FrostingTask
{
}

internal static class BuildProcess
{
    public static void Run(BuildContext context, string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = context.RootDirectory,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start '{fileName}'.");

        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"'{fileName}' exited with code {process.ExitCode}.");
        }
    }

    public static string Capture(BuildContext context, string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = context.RootDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start '{fileName}'.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"'{fileName}' exited with code {process.ExitCode}: {error}");
        }

        return output;
    }

    public static string FindMsBuildPath(BuildContext context)
    {
        var output = Capture(context, "dotnet", "--list-sdks");
        var sdk = output
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => Regex.Match(line, @"^(?<version>\S+) \[(?<path>.+)\]$"))
            .Where(match => match.Success)
            .Select(match => new
            {
                Version = Version.Parse(match.Groups["version"].Value.Split('-')[0]),
                Path = match.Groups["path"].Value,
                Name = match.Groups["version"].Value,
            })
            .OrderByDescending(sdk => sdk.Version)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Could not find an installed .NET SDK.");

        return Path.Combine(sdk.Path, sdk.Name, "MSBuild.dll");
    }
}

// Content definitions are edited by hand, so they are kept one key per line: a one-line file
// makes every edit a whole-file diff and two people touching different keys a merge conflict.
// The canonical shape is whatever Utf8JsonWriter's indented mode writes (two spaces, matching
// .editorconfig), with LF line endings and a trailing newline.
internal static class ContentJson
{
    public static IEnumerable<string> Files(BuildContext context) =>
        Directory.EnumerateFiles(context.ContentDirectory, "*.json", SearchOption.AllDirectories)
            .Where(path => !path.StartsWith(context.TerrainContentDirectory, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

    public static string Format(string json)
    {
        using var document = JsonDocument.Parse(json);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            document.WriteTo(writer);
        }

        // .NET 8 has no NewLine option on the writer and uses Environment.NewLine.
        return Encoding.UTF8.GetString(buffer.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    public static IReadOnlyList<string> Misformatted(BuildContext context) =>
        Files(context)
            .Where(path => File.ReadAllText(path) != Format(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(context.RootDirectory, path))
            .ToList();
}

internal static class TextureImports
{
    // Drawn only as the boot splash, flat on the screen at its own size, where a mip chain is
    // never sampled.
    private static readonly string[] ScreenOnly = ["splash/title_page.png.import"];

    // Every sprite is minified on screen, and its engraved hatching shimmers and reads sharper
    // than its neighbours without a mip chain. The project's importer default covers new files;
    // this catches one imported before that default, or switched off by hand.
    public static IReadOnlyList<string> WithoutMipmaps(BuildContext context) =>
        Directory.EnumerateFiles(context.ContentDirectory, "*.png.import", SearchOption.AllDirectories)
            .Where(path => !ScreenOnly.Contains(Path.GetRelativePath(context.ContentDirectory, path).Replace('\\', '/'), StringComparer.Ordinal))
            .Where(path => !File.ReadLines(path).Contains("mipmaps/generate=true", StringComparer.Ordinal))
            .Select(path => Path.GetRelativePath(context.RootDirectory, path))
            .Order(StringComparer.Ordinal)
            .ToList();
}
