using ManyWinters.Godot.Sprites;

namespace ManyWinters.Godot.Logic;

// Combining the visible extents of the layers a single entity is drawn from - see
// SpriteVisibleExtent for what an extent is and why the nominal canvas won't do.
internal static class SpriteExtents
{
    // The true silhouette of a split tree is the union of its trunk's and canopy's own visible
    // extents - equivalent to what a single combined image's extent already was, since the two
    // are an exact partition of it (see split_trunk_canopy).
    internal static SpriteVisibleExtent.Extent Combine(SpriteVisibleExtent.Extent a, SpriteVisibleExtent.Extent b)
    {
        var minX = Math.Min(a.CenterXOffset - (a.Width / 2f), b.CenterXOffset - (b.Width / 2f));
        var maxX = Math.Max(a.CenterXOffset + (a.Width / 2f), b.CenterXOffset + (b.Width / 2f));
        var minY = Math.Min(a.CenterYOffset - (a.Height / 2f), b.CenterYOffset - (b.Height / 2f));
        var maxY = Math.Max(a.CenterYOffset + (a.Height / 2f), b.CenterYOffset + (b.Height / 2f));
        return new SpriteVisibleExtent.Extent(maxX - minX, maxY - minY, (minX + maxX) / 2f, (minY + maxY) / 2f);
    }
}
