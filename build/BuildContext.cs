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
}
