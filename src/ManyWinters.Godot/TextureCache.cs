using Godot;

namespace ManyWinters.Godot;

// A tight loop that calls ResourceLoader.Load<T> for the same already-cached path thousands
// of times in a row (spawning thousands of ResourceNodeViews of a small handful of distinct
// kinds - see MapLoader.ScatterDecorations) reliably crashed Godot's C# bridge on startup: a
// GCHandle race in ScriptManagerBridge.SwapGCHandleForType ("Handle is not initialized").
// Godot's own engine-side resource cache avoids the disk read on a repeat path, but each call
// still re-wraps the native resource in a fresh C# object, and doing that re-wrap thousands of
// times back-to-back on the same resource isn't safe. Caching per path here means each unique
// texture is only ever wrapped once, no matter how many entities share it.
public static class TextureCache
{
    private static readonly Dictionary<string, Texture2D?> Cache = new();

    // For a path that's expected to exist (a known content file) - throws the same way a bare
    // ResourceLoader.Load call would if it doesn't.
    public static Texture2D Get(string path)
    {
        if (Cache.TryGetValue(path, out var cached) && cached is not null)
        {
            return cached;
        }

        var texture = ResourceLoader.Load<Texture2D>(path);
        Cache[path] = texture;
        return texture;
    }

    // For a path that might legitimately be missing (a kind with no art yet) - null instead of
    // throwing, so the caller can fall back to a placeholder.
    public static Texture2D? TryGet(string path)
    {
        if (Cache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var texture = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
        Cache[path] = texture;
        return texture;
    }
}
