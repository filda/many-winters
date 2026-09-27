using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Sprites;

// Reads the non-transparent rectangle out of a sprite's texture, which needs the engine, and
// converts it to world metres.
internal static class SpriteVisibleExtent
{
    // Keyed by texture path: the used rect and canvas size are fractions of the image, so one
    // read covers every instance sharing the texture whatever its worldHeight.
    private static readonly Dictionary<string, (Vector2 Position, Vector2 Size, Vector2 CanvasSize)> _cache = new();

    internal static SpriteExtents.Extent Compute(string texturePath, float worldHeight)
    {
        if (!_cache.TryGetValue(texturePath, out var used))
        {
            if (!ResourceLoader.Exists(texturePath))
            {
                // No art for this kind yet: the billboard falls back to a flat opaque quad the
                // full size of the canvas, so the used rect must be the whole square rather than
                // an empty read. Checked before TextureCache.Get, not after it throws: Get wraps
                // a bare ResourceLoader.Load, which returns null for a missing resource, and
                // GetImage() on that null crashed for every entity missing its texture.
                used = (Vector2.Zero, Vector2.One, Vector2.One);
            }
            else
            {
                using var image = TextureCache.Get(texturePath).GetImage();
                var usedRect = image.GetUsedRect();
                used = (
                    new Vector2(usedRect.Position.X, usedRect.Position.Y),
                    new Vector2(usedRect.Size.X, usedRect.Size.Y),
                    new Vector2(image.GetWidth(), image.GetHeight()));
            }

            _cache[texturePath] = used;
        }

        return SpriteExtents.From(used.Position, used.Size, used.CanvasSize, worldHeight);
    }
}
