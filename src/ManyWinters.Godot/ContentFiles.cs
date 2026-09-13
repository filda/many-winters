using Godot;

// Godot.FileAccess and System.IO.FileAccess collide under the project's implicit usings.
using GodotFileAccess = Godot.FileAccess;

namespace ManyWinters.Godot;

// Content is a real directory under the editor but sits inside ManyWinters.pck once exported,
// where System.IO cannot see it (ProjectSettings.GlobalizePath returns a path next to the .exe
// that does not exist). Godot's own file access resolves res:// in both cases; this is the
// resource loader's equivalent for the .json files Core parses itself.
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
