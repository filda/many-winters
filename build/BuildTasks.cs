using System.Diagnostics;
using System.Text.RegularExpressions;
using Cake.Frosting;

namespace ManyWinters.Build;

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
    public override void Run(BuildContext context)
    {
        BuildProcess.Run(context, "dotnet", "test", context.SolutionPath, "--no-build", "--configuration", context.BuildConfiguration, "--verbosity", "normal");
    }
}

[TaskName("InspectCode")]
[IsDependentOn(typeof(BuildTask))]
public sealed class InspectCodeTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var report = Path.Combine(Path.GetTempPath(), "many-winters-inspectcode.xml");
        var arguments = new List<string>
        {
            "jb", "inspectcode", context.SolutionPath, "--swea", "--no-build", "--severity=WARNING",
            "--properties:Configuration=" + context.BuildConfiguration, "-f=Xml", "-o=" + report,
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

[TaskName("CI")]
[IsDependentOn(typeof(LineEndingsTask))]
[IsDependentOn(typeof(RestoreTask))]
[IsDependentOn(typeof(FormatTask))]
[IsDependentOn(typeof(BuildTask))]
[IsDependentOn(typeof(InspectCodeTask))]
[IsDependentOn(typeof(TestTask))]
public sealed class CiTask : FrostingTask
{
}
