using Cake.Core;
using Cake.Frosting;

namespace ManyWinters.Build;

public sealed class BuildContext(ICakeContext context) : FrostingContext(context)
{
    public string RootDirectory { get; } = Directory.GetCurrentDirectory();

    public string BuildConfiguration => Arguments.GetArgument("configuration") ?? "Release";

    public string SolutionPath => Path.Combine(RootDirectory, "ManyWinters.sln");

    public string BuildProjectPath => Path.Combine(RootDirectory, "build", "ManyWinters.Build.csproj");

    public string GodotProjectPath => Path.Combine(RootDirectory, "src", "ManyWinters.Godot");

    // Not part of ManyWinters.sln — see the comment atop the csproj for why.
    public string EndToEndTestProjectPath => Path.Combine(RootDirectory, "src", "ManyWinters.E2E.Tests", "ManyWinters.E2E.Tests.csproj");

    // Reports a failing task leaves behind for a human to read. Inside the repository rather than
    // the temp directory, so ci.yml can upload the folder as a job artifact; git ignores it.
    public string ArtifactsDirectory => Path.Combine(RootDirectory, "artifacts");
}
