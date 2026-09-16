using System.Diagnostics;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace ManyWinters.Build;

internal static partial class BuildProcess
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
[IsDependentOn(typeof(RestoreTask))]
[IsDependentOn(typeof(FormatTask))]
[IsDependentOn(typeof(BuildTask))]
[IsDependentOn(typeof(InspectCodeTask))]
[IsDependentOn(typeof(TestTask))]
public sealed class CiTask : FrostingTask
{
}

[TaskName("Beckett")]
public sealed class BeckettTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var port = ReadPort(context);
        if (!IsListening(port))
        {
            var startInfo = new ProcessStartInfo("godot")
            {
                WorkingDirectory = context.RootDirectory,
                UseShellExecute = true,
            };
            startInfo.ArgumentList.Add("-e");
            startInfo.ArgumentList.Add("--path");
            startInfo.ArgumentList.Add(context.GodotProjectPath);

            _ = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start Godot.");
        }

        var deadline = DateTime.UtcNow.AddSeconds(180);
        while (!IsListening(port))
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException($"Beckett did not start listening on port {port}.");
            }

            Thread.Sleep(TimeSpan.FromSeconds(2));
        }

        var tokenPath = Path.Combine(context.GodotProjectPath, ".beckett", "token");
        var token = File.ReadAllText(tokenPath).Trim();
        context.Log.Information(Verbosity.Normal, "Beckett is ready at http://127.0.0.1:{0}/mcp/{1}", port, token);
    }

    private static int ReadPort(BuildContext context)
    {
        var path = Path.Combine(context.GodotProjectPath, ".beckett", "port");
        return File.Exists(path) && int.TryParse(File.ReadAllText(path).Trim(), out var port) ? port : 8770;
    }

    private static bool IsListening(int port)
    {
        try
        {
            using var client = new TcpClient();
            return client.ConnectAsync("127.0.0.1", port).Wait(TimeSpan.FromMilliseconds(250));
        }
        catch (SocketException)
        {
            return false;
        }
    }
}

internal static partial class BuildProcess
{
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

    private static string Capture(BuildContext context, string fileName, params string[] arguments)
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
}
