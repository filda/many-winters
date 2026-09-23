using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace ManyWinters.Build;

/// <summary>
/// Runs the windowed, screenshot-driven end-to-end suite (src/ManyWinters.E2E.Tests) — golden
/// paths through the actual rendered game, distinct from the fast <c>Test</c> target's headless
/// unit/integration coverage. Part of the <c>CI</c> target so it can't silently rot, but it can
/// only ever drive a real window (PrintWindow/PostMessage), so it skips itself rather than
/// failing on any other OS; the <c>e2e-windows</c> CI job is what actually runs it.
/// </summary>
[TaskName("E2E")]
[IsDependentOn(typeof(BuildTask))]
public sealed class E2ETask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        if (!OperatingSystem.IsWindows())
        {
            context.Log.Information(Verbosity.Normal, "Skipping E2E: Windows only (PrintWindow/PostMessage). Run it on the e2e-windows CI job or a Windows machine.");
            return;
        }

        BuildProcess.Run(context, "dotnet", "test", context.EndToEndTestProjectPath, "--configuration", context.BuildConfiguration, "--verbosity", "normal");
    }
}
