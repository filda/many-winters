using Godot;

namespace ManyWinters.Godot.Logic;

// The part of a sprite's square canvas that actually has ink on it, in world metres. Content
// never fills the whole canvas, so a collision shape or anchor sized off the nominal canvas is
// an oversized, misaligned box. Reading the used rect needs the engine (SpriteVisibleExtent,
// which caches it); converting to metres and merging layers does not.
internal static class SpriteExtents
{
    // CenterXOffset/CenterYOffset: how far the visible content's centre sits right (+X) of and
    // above (+Y) the sprite node's origin. Content is not always centred in its canvas, so a
    // caller placing a shape off "the sprite's centre" needs these, not just the size.
    internal readonly record struct Extent(float Width, float Height, float CenterXOffset, float CenterYOffset);

    // `worldHeight` is what the sprite was created at: the whole canvas height maps to it and
    // everything else scales from there.
    internal static Extent From(Vector2 usedPosition, Vector2 usedSize, Vector2 canvasSize, float worldHeight)
    {
        var metresPerPixel = worldHeight / canvasSize.Y;
        var usedCentre = usedPosition + (usedSize / 2f);
        var canvasCentre = canvasSize / 2f;

        return new Extent(
            usedSize.X * metresPerPixel,
            usedSize.Y * metresPerPixel,
            // Image columns count rightward, like the sprite's local right: no sign flip.
            (usedCentre.X - canvasCentre.X) * metresPerPixel,
            // Image rows count downward and the sprite's local up is the opposite, hence the flip.
            (canvasCentre.Y - usedCentre.Y) * metresPerPixel);
    }

    // The extent as rendered right now. From() answers for the creation height, which is what
    // SpriteVisibleExtent caches per texture, so it is blind to any scale applied since - and
    // every person and tree is scaled from its seed. Unscaled, an anchor floats above a short
    // person and sinks into a tall one.
    //
    // Same arithmetic as BillboardUv.RenderedSize by construction: BillboardSprite.Apply sets
    // PixelSize = worldHeight / canvasHeight. SpriteExtentsTests pins that they agree.
    internal static Extent Scaled(Extent extent, float scaleX, float scaleY) => new(
        extent.Width * scaleX,
        extent.Height * scaleY,
        extent.CenterXOffset * scaleX,
        extent.CenterYOffset * scaleY);

    // The silhouette of a split tree is the union of its trunk's and canopy's extents - the
    // same as a single combined image's, since the two partition it.
    internal static Extent Combine(Extent a, Extent b)
    {
        var minX = Math.Min(a.CenterXOffset - (a.Width / 2f), b.CenterXOffset - (b.Width / 2f));
        var maxX = Math.Max(a.CenterXOffset + (a.Width / 2f), b.CenterXOffset + (b.Width / 2f));
        var minY = Math.Min(a.CenterYOffset - (a.Height / 2f), b.CenterYOffset - (b.Height / 2f));
        var maxY = Math.Max(a.CenterYOffset + (a.Height / 2f), b.CenterYOffset + (b.Height / 2f));

        return new Extent(maxX - minX, maxY - minY, (minX + maxX) / 2f, (minY + maxY) / 2f);
    }
}
