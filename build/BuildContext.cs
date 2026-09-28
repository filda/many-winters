using Cake.Core;
using Cake.Frosting;

namespace ManyWinters.Build;

public sealed class BuildContext(ICakeContext context) : FrostingContext(context)
{
    public string RootDirectory { get; } = Directory.GetCurrentDirectory();

    public string BuildConfiguration => Arguments.GetArgument("configuration") ?? "Release";

    // Null unless given: the Test target then draws one of its own.
    public string? TestOrderSeed => Arguments.GetArgument("seed");

    public string SolutionPath => Path.Combine(RootDirectory, "ManyWinters.sln");

    public string BuildProjectPath => Path.Combine(RootDirectory, "build", "ManyWinters.Build.csproj");

    public string GodotProjectPath => Path.Combine(RootDirectory, "src", "ManyWinters.Godot");

    public string SynthPrototypeProjectPath => Path.Combine(RootDirectory, "src", "ManyWinters.Tools", "SynthPrototype", "ManyWinters.Tools.SynthPrototype.csproj");

    // The test projects inside ManyWinters.sln, which the Test target runs one by one.
    public IReadOnlyList<string> UnitTestProjectPaths =>
    [
        Path.Combine(RootDirectory, "src", "ManyWinters.Tests", "ManyWinters.Tests.csproj"),
        Path.Combine(RootDirectory, "src", "ManyWinters.Godot.Tests", "ManyWinters.Godot.Tests.csproj"),
    ];

    // Not part of ManyWinters.sln — see the comment atop the csproj for why.
    public string EndToEndTestProjectPath => Path.Combine(RootDirectory, "src", "ManyWinters.E2E.Tests", "ManyWinters.E2E.Tests.csproj");

    // Reports a failing task leaves behind for a human to read. Inside the repository rather than
    // the temp directory, so ci.yml can upload the folder as a job artifact; git ignores it.
    public string ArtifactsDirectory => Path.Combine(RootDirectory, "artifacts");

    // InspectCode's per-solution cache. Left to its default it lands under %LOCALAPPDATA% and
    // outlives the checkout it describes; a stale one reports "Cannot resolve symbol" on code
    // that builds. Kept here so InspectCodeClean knows what to delete.
    public string InspectCodeCacheDirectory => Path.Combine(ArtifactsDirectory, "inspectcode-cache");
}
