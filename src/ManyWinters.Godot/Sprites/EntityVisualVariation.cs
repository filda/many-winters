using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot.Sprites;

// Deterministic per-instance variety: same seed (an entity's stable id, via EntityId.SeedOf)
// always produces the same tint/scale, so repeated saves/reloads don't reshuffle how things
// look, but instances of the same kind don't render as identical clones either.
public static class EntityVisualVariation
{
    public static Color Tint(Color baseColor, int seed)
    {
        var random = new Random(seed);
        var hueShift = ((float)random.NextDouble() - 0.5f) * 0.08f;
        var valueShift = ((float)random.NextDouble() - 0.5f) * 0.3f;

        var hue = Mathf.PosMod(baseColor.H + hueShift, 1f);
        var value = Mathf.Clamp(baseColor.V + valueShift, 0f, 1f);
        return Color.FromHsv(hue, baseColor.S, value, baseColor.A);
    }

    public static float Scale(int seed, float minScale, float maxScale)
    {
        var random = new Random(seed);
        return minScale + ((float)random.NextDouble() * (maxScale - minScale));
    }

    // Like Scale, but for callers that need several independent attributes off the same
    // seed (e.g. a person's walk-cycle rate and its bob/rock amplitudes, all keyed off their
    // id) - a distinguishing salt per attribute avoids each one just landing on the same
    // underlying draw, rescaled differently. Also avalanches seed+salt first (Thomas Wang's
    // 32-bit integer hash): System.Random's legacy algorithm correlates badly on adjacent
    // small integer seeds, and two ids' seeds can land anywhere, close together included.
    public static float RangeFor(int seed, int salt, float min, float max)
    {
        var random = new Random(Avalanche(seed, salt));
        return min + ((float)random.NextDouble() * (max - min));
    }

    // Same seed+salt avalanche as RangeFor, but for picking one of several discrete
    // options (a hairstyle, a clothing style, ...) rather than a continuous value.
    public static int IndexFor(int seed, int salt, int count)
    {
        var random = new Random(Avalanche(seed, salt));
        return random.Next(count);
    }

    private static int Avalanche(int seed, int salt) =>
        SeedHash.Avalanche(unchecked(((uint)seed * 0x9E3779B1u) + (uint)salt));
}
