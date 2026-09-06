using ManyWinters.Core.World;
using ManyWinters.Tools.SimulationRunner;

// The same content the Godot game ships with, so a headless run plays by the same rules.
// The default assumes the repository root as the working directory, which is how the README
// invokes it; anything else says where the content lives with --content.
var contentRoot = "src/ManyWinters.Godot/Content";
var commands = args;
if (args.Length >= 2 && args[0] == "--content")
{
    contentRoot = args[1];
    commands = args[2..];
}

if (commands.Length == 0)
{
    Console.WriteLine("Usage: dotnet run --project src/ManyWinters.Tools/SimulationRunner -- [--content <dir>] <command> [<command> ...]");
    Console.WriteLine("Options:");
    Console.WriteLine("  --content <dir>     Content folder to load catalogs from (default: src/ManyWinters.Godot/Content)");
    Console.WriteLine("Commands:");
    Console.WriteLine("  generate            Start a fresh world");
    Console.WriteLine("  create <n>          Add n people to the current world");
    Console.WriteLine("  simulate <ticks>    Advance the simulation clock by <ticks>");
    Console.WriteLine("  print population    Print the current tick and every person");
    Console.WriteLine("  save <path>         Save the current world to <path>");
    Console.WriteLine("  load <path>         Load a world from <path>");
    return 0;
}

if (!Directory.Exists(contentRoot))
{
    Console.Error.WriteLine($"Content folder '{contentRoot}' not found. Run from the repository root, or pass --content <dir>.");
    return 1;
}

var script = new SimulationScript(WorldConfiguration.LoadFromDirectory(contentRoot));
foreach (var line in script.Run(SimulationScript.SplitIntoCommands(commands)))
{
    Console.WriteLine(line);
}

return 0;
