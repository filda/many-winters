using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Sprites;

// Reads the non-transparent rectangle out of a sprite's texture, which needs the engine, and
// hands it to SpriteExtents to turn into world metres.
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
                // No art for this kind (yet): BillboardSprite.Apply falls back to a flat quad the
                // full size of the canvas, opaque everywhere, so the visible extent is the whole
                // square rather than a used-ink rect there is no image here to read. Checked
                // before TextureCache.Get, not after it throws: Get wraps a bare
                // ResourceLoader.Load, which would print Godot's own "Resource file not found"
                // and hand back null, and GetImage() on that null crashed AnimalView._Ready
                // (RefreshCollisionShape -> VisibleExtent -> Compute) for every animal missing its
                // species texture (docs/todo/fauna-plan.md phase 2b).
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
