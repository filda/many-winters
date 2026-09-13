using System.Text.Json;

namespace ManyWinters.Core.Serialization;

// Every content catalog is a directory of <id>/<id>.json files, loadable two ways: off the
// filesystem (tests, SimulationRunner), or from documents Godot already read, because an
// exported build keeps content inside ManyWinters.pck where System.IO cannot see it. Splitting
// "find" from "parse" keeps Core Godot-free.
public static class JsonDefinitions
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static IEnumerable<(string Source, string Json)> ReadDirectory(string rootPath)
    {
        foreach (var directory in Directory.GetDirectories(rootPath))
        {
            foreach (var file in Directory.GetFiles(directory, "*.json"))
            {
                yield return (file, File.ReadAllText(file));
            }
        }
    }

    // `kind` only ever reaches a human, in the exception message naming what failed to parse.
    public static List<T> Parse<T>(IEnumerable<(string Source, string Json)> documents, string kind)
    {
        var definitions = new List<T>();

        foreach (var (source, json) in documents)
        {
            definitions.Add(JsonSerializer.Deserialize<T>(json, Options)
                ?? throw new InvalidDataException($"{kind} definition '{source}' could not be parsed."));
        }

        return definitions;
    }
}
