using System.Text.Json;

namespace ManyWinters.Core.Serialization;

// Every content catalog is the same shape on disk - a directory of <id>/<id>.json files - and
// every one of them has to be loadable two ways. Straight off the filesystem is what the tests
// and the headless SimulationRunner want. Reading documents someone else already opened is what
// the Godot build needs: once exported, content lives inside ManyWinters.pck and System.IO
// cannot see it at all, so only Godot's own file access can reach it. Splitting "find the
// files" from "parse the files" is what lets Core stay Godot-free while still being usable
// from inside an exported game.
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
