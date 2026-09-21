using Godot;

namespace ManyWinters.Godot.Sprites;

// Calling ResourceLoader.Load<T> thousands of times back-to-back for one path (thousands of
// decoration views sharing a few textures) crashes the C# bridge with a GCHandle race in
// ScriptManagerBridge.SwapGCHandleForType: the engine cache skips the disk read, but each call
// still wraps the native resource in a fresh C# object. Caching per path wraps each texture
// exactly once.
public static class TextureCache
{
    private static readonly Dictionary<string, Texture2D?> Cache = new();

    // For a path expected to exist - throws like a bare ResourceLoader.Load if it doesn't.
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

    // For a path that may legitimately be missing (a kind with no art yet) - null instead of
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
