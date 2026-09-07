using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Sprites;

// Reads the non-transparent rectangle out of a sprite's texture, which needs the engine, and
// hands it to SpriteExtents to turn into world metres.
internal static class SpriteVisibleExtent
{
    // Keyed by texture path: the used rect and the canvas size are resolution-independent
    // fractions of the image, so one read per unique texture covers every instance that shares
    // it (every conifer tree on the map, say) whatever its own worldHeight.
    private static readonly Dictionary<string, (Vector2 Position, Vector2 Size, Vector2 CanvasSize)> _cache = new();

    internal static SpriteExtents.Extent Compute(string texturePath, float worldHeight)
    {
        if (!_cache.TryGetValue(texturePath, out var used))
        {
            using var image = TextureCache.Get(texturePath).GetImage();
            var usedRect = image.GetUsedRect();
            used = (
                new Vector2(usedRect.Position.X, usedRect.Position.Y),
                new Vector2(usedRect.Size.X, usedRect.Size.Y),
                new Vector2(image.GetWidth(), image.GetHeight()));
            _cache[texturePath] = used;
        }

        return SpriteExtents.From(used.Position, used.Size, used.CanvasSize, worldHeight);
    }
}
