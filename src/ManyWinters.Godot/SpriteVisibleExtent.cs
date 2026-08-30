using Godot;

namespace ManyWinters.Godot;

// The actual non-transparent extent of a sprite texture, in world meters, for a given
// worldHeight (the same value passed to BillboardSprite.Create) - every one of our textures
// is a square canvas with real content occupying only part of it (a canopy doesn't fill the
// corners, a standing figure doesn't fill the full height), so collision shapes and anchor
// points sized/positioned off the nominal canvas dimensions read as an oversized, misaligned
// bounding box rather than a pixel-accurate one.
public static class SpriteVisibleExtent
{
    // Keyed by texture path; the used-rect and texture size are resolution-independent
    // fractions of the canvas, so one computation per unique texture covers every instance
    // that shares it (e.g. every conifer tree on the map) regardless of its own worldHeight.
    private static readonly Dictionary<string, (Vector2 Position, Vector2 Size, Vector2 CanvasSize)> _cache = new();

    public readonly record struct Extent(float Width, float Height, float CenterXOffset, float CenterYOffset);

    // CenterXOffset/CenterYOffset are how far the visible content's own center sits to the
    // side of (+X = right) and above (+Y = up) the sprite node's origin - content isn't
    // always centered in its canvas (a tree's canopy sits higher than its trunk's midpoint;
    // a figure's silhouette isn't necessarily centered left-right either), so callers
    // positioning a collision shape or an anchor off of "the sprite's center" need this, not
    // just size.
    public static Extent Compute(string texturePath, float worldHeight)
    {
        if (!_cache.TryGetValue(texturePath, out var normalized))
        {
            var texture = TextureCache.Get(texturePath);
            using var image = texture.GetImage();
            var loadedCanvasSize = new Vector2(image.GetWidth(), image.GetHeight());
            var usedRect = image.GetUsedRect();
            normalized = (new Vector2(usedRect.Position.X, usedRect.Position.Y), new Vector2(usedRect.Size.X, usedRect.Size.Y), loadedCanvasSize);
            _cache[texturePath] = normalized;
        }

        var (position, size, canvasSize) = normalized;
        var pixelSize = worldHeight / canvasSize.Y;
        var usedCenterX = position.X + (size.X / 2f);
        var usedCenterY = position.Y + (size.Y / 2f);
        var canvasCenterX = canvasSize.X / 2f;
        var canvasCenterY = canvasSize.Y / 2f;
        // Image columns count rightward, same direction as the sprite's own local right, so
        // this needs no sign flip - unlike rows (see below), which do.
        var centerXOffset = (usedCenterX - canvasCenterX) * pixelSize;
        // Image rows count downward; the sprite's own local up is the opposite direction,
        // so a used-rect center below the canvas center (usedCenterY > canvasCenterY) means
        // the visible content actually sits below the node's origin.
        var centerYOffset = (canvasCenterY - usedCenterY) * pixelSize;
        return new Extent(size.X * pixelSize, size.Y * pixelSize, centerXOffset, centerYOffset);
    }
}
