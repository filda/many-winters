using Godot;

namespace ManyWinters.Godot.Logic;

// What part of a sprite's square canvas actually has ink on it, in world metres. Every texture
// here leaves real content occupying only part of its canvas - a canopy does not fill the
// corners, a standing figure does not fill the full height - so a collision shape or an anchor
// sized off the nominal canvas reads as an oversized, misaligned box rather than a
// pixel-accurate one.
//
// Reading the used rect off an image needs the engine (SpriteVisibleExtent does that and
// caches it); turning one into metres, and merging the extents of a sprite drawn in layers,
// does not.
internal static class SpriteExtents
{
    // CenterXOffset/CenterYOffset are how far the visible content's own centre sits to the side
    // of (+X = right) and above (+Y = up) the sprite node's origin - content is not always
    // centred in its canvas (a tree's canopy sits higher than its trunk's midpoint, and a
    // figure's silhouette need not be centred left to right), so a caller placing a collision
    // shape or an anchor off "the sprite's centre" needs this and not just the size.
    internal readonly record struct Extent(float Width, float Height, float CenterXOffset, float CenterYOffset);

    // `worldHeight` is what the sprite was created at (see BillboardSprite.Create), so the
    // whole canvas height maps to it and everything else scales from there.
    internal static Extent From(Vector2 usedPosition, Vector2 usedSize, Vector2 canvasSize, float worldHeight)
    {
        var metresPerPixel = worldHeight / canvasSize.Y;
        var usedCentre = usedPosition + (usedSize / 2f);
        var canvasCentre = canvasSize / 2f;

        return new Extent(
            usedSize.X * metresPerPixel,
            usedSize.Y * metresPerPixel,
            // Image columns count rightward, the same direction as the sprite's own local
            // right, so this needs no sign flip - unlike rows, which do.
            (usedCentre.X - canvasCentre.X) * metresPerPixel,
            // Image rows count downward and the sprite's local up is the opposite, so content
            // whose centre sits below the canvas centre really is below the node's origin.
            (canvasCentre.Y - usedCentre.Y) * metresPerPixel);
    }

    // The same extent as the sprite is actually rendering right now. From() answers for the
    // height the sprite was *created* at, which is what SpriteVisibleExtent caches per texture
    // and is therefore blind to any scale applied since - and hover scales a sprite by a tenth
    // (HoverHighlight.ScaleFactor). Left unscaled, a collision box or a marker anchor derived
    // from the nominal extent sits a tenth out of step with the pixels on screen for exactly
    // as long as the cursor is on the thing, which is the one moment it has to be right.
    //
    // This is deliberately the same arithmetic as BillboardUv.RenderedSize, and the two agree
    // by construction rather than by coincidence: BillboardSprite.Apply sets
    // PixelSize = worldHeight / canvasHeight, so a texture's own pixels times PixelSize times
    // scale is the same metres From() derives from worldHeight and then this multiplies by the
    // same scale. SpriteExtentsTests pins that they do not drift apart.
    internal static Extent Scaled(Extent extent, float scaleX, float scaleY) => new(
        extent.Width * scaleX,
        extent.Height * scaleY,
        extent.CenterXOffset * scaleX,
        extent.CenterYOffset * scaleY);

    // The true silhouette of a split tree is the union of its trunk's and canopy's own visible
    // extents - equivalent to what a single combined image's extent already was, since the two
    // are an exact partition of it (see split_trunk_canopy).
    internal static Extent Combine(Extent a, Extent b)
    {
        var minX = Math.Min(a.CenterXOffset - (a.Width / 2f), b.CenterXOffset - (b.Width / 2f));
        var maxX = Math.Max(a.CenterXOffset + (a.Width / 2f), b.CenterXOffset + (b.Width / 2f));
        var minY = Math.Min(a.CenterYOffset - (a.Height / 2f), b.CenterYOffset - (b.Height / 2f));
        var maxY = Math.Max(a.CenterYOffset + (a.Height / 2f), b.CenterYOffset + (b.Height / 2f));

        return new Extent(maxX - minX, maxY - minY, (minX + maxX) / 2f, (minY + maxY) / 2f);
    }
}
