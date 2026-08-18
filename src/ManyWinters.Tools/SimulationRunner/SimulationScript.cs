using ManyWinters.Core.Persistence;
using ManyWinters.Core.World;

namespace ManyWinters.Tools.SimulationRunner;

public sealed class SimulationScript
{
    private static readonly HashSet<string> Verbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "generate", "create", "simulate", "print", "save", "load",
    };

    public WorldState World { get; private set; } = new();

    // Re-chunks unquoted shell argv (e.g. "create 2 simulate 100") back into individual commands by splitting at each verb.
    public static IReadOnlyList<string> SplitIntoCommands(IReadOnlyList<string> tokens)
    {
        var commands = new List<string>();
        var current = new List<string>();

        foreach (var token in tokens)
        {
            if (current.Count > 0 && Verbs.Contains(token))
            {
                commands.Add(string.Join(' ', current));
                current.Clear();
            }

            current.Add(token);
        }

        if (current.Count > 0)
        {
            commands.Add(string.Join(' ', current));
        }

        return commands;
    }

    public IReadOnlyList<string> Run(IEnumerable<string> commands)
    {
        var output = new List<string>();
        foreach (var command in commands)
        {
            output.AddRange(Execute(command));
        }

        return output;
    }

    private List<string> Execute(string command)
    {
        var output = new List<string>();
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return output;
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "generate":
                World = new WorldState();
                output.Add("Generated a new world.");
                break;

            case "create":
                if (!TryParseCount(parts, out var createCount))
                {
                    output.Add($"Invalid command: '{command}'. Usage: create <n>");
                    break;
                }

                for (var i = 0; i < createCount; i++)
                {
                    World.AddPerson($"Person {World.People.Count + 1}", new Position(0, 0));
                }

                output.Add($"Created {createCount} people. Population is now {World.People.Count}.");
                break;

            case "simulate":
                if (!TryParseCount(parts, out var ticks))
                {
                    output.Add($"Invalid command: '{command}'. Usage: simulate <ticks>");
                    break;
                }

                World.Clock.Advance(ticks);
                output.Add($"Advanced {ticks} ticks. Current tick is {World.Clock.CurrentTick}.");
                break;

            case "print" when parts.Length > 1 && parts[1].Equals("population", StringComparison.OrdinalIgnoreCase):
                output.Add($"Tick {World.Clock.CurrentTick}: {World.People.Count} people alive.");
                foreach (var person in World.People)
                {
                    output.Add($"  {person.Id} {person.Name} at {person.Position}");
                }

                break;

            case "save" when parts.Length > 1:
                SaveGameService.Save(World, parts[1]);
                output.Add($"Saved to {parts[1]}.");
                break;

            case "load" when parts.Length > 1:
                World = SaveGameService.Load(parts[1]);
                output.Add($"Loaded from {parts[1]}. Tick {World.Clock.CurrentTick}, {World.People.Count} people.");
                break;

            default:
                output.Add($"Unknown command: '{command}'.");
                break;
        }

        return output;
    }

    private static bool TryParseCount(string[] parts, out int value)
    {
        value = 0;
        return parts.Length > 1 && int.TryParse(parts[1], out value);
    }
}
