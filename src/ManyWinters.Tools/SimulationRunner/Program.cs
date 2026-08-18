using ManyWinters.Tools.SimulationRunner;

if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run --project src/ManyWinters.Tools/SimulationRunner -- <command> [<command> ...]");
    Console.WriteLine("Commands:");
    Console.WriteLine("  generate            Start a fresh world");
    Console.WriteLine("  create <n>          Add n people to the current world");
    Console.WriteLine("  simulate <ticks>    Advance the simulation clock by <ticks>");
    Console.WriteLine("  print population    Print the current tick and every person");
    Console.WriteLine("  save <path>         Save the current world to <path>");
    Console.WriteLine("  load <path>         Load a world from <path>");
    return;
}

var script = new SimulationScript();
foreach (var line in script.Run(SimulationScript.SplitIntoCommands(args)))
{
    Console.WriteLine(line);
}
