using Godot;

// Godot.FileAccess and System.IO.FileAccess collide under the project's implicit usings.
using GodotFileAccess = Godot.FileAccess;

namespace ManyWinters.Godot;

// Content sits in a real directory while the editor runs and inside ManyWinters.pck once the
// game is exported, and System.IO only ever sees the first of those - ProjectSettings.Globalize
// Path happily hands back a path next to the .exe that does not exist. Everything the game
// reads as data therefore has to go through Godot's own file access, which resolves res://
// identically in both cases. Textures and .tres files already do, via the resource loader;
// this is the equivalent for the plain .json files Core parses itself.
public static class ContentFiles
{
    // Matches the <root>/<id>/<id>.json layout every catalog directory uses.
    public static IEnumerable<(string Source, string Json)> ReadJsonTree(string resourceDirectory)
    {
        foreach (var subdirectory in DirAccess.GetDirectoriesAt(resourceDirectory))
        {
            var directoryPath = $"{resourceDirectory}/{subdirectory}";

            foreach (var file in DirAccess.GetFilesAt(directoryPath))
            {
                if (!file.EndsWith(".json", StringComparison.Ordinal))
                {
                    continue;
                }

                var filePath = $"{directoryPath}/{file}";
                yield return (filePath, ReadText(filePath));
            }
        }
    }

    public static string ReadText(string resourcePath)
    {
        using var file = GodotFileAccess.Open(resourcePath, GodotFileAccess.ModeFlags.Read);

        return file is null
            ? throw new FileNotFoundException($"Could not open '{resourcePath}': {GodotFileAccess.GetOpenError()}.")
            : file.GetAsText();
    }

    public static bool Exists(string resourcePath) => GodotFileAccess.FileExists(resourcePath);
}
