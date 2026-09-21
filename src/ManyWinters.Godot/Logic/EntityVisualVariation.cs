using Godot;
using ManyWinters.Core.World;

namespace ManyWinters.Godot.Logic;

// Deterministic per-instance variety: the same seed, derived from an entity's id, always gives
// the same tint/scale, so reloads do not reshuffle looks, yet instances of one kind are not
// identical clones.
internal static class EntityVisualVariation
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

    // Like Scale, but for several independent attributes off one seed (a person's walk rate
    // and bob amplitude): a salt per attribute keeps them from being the same draw rescaled.
    // seed+salt is avalanched first because System.Random correlates badly on nearby seeds.
    public static float RangeFor(int seed, int salt, float min, float max)
    {
        var random = new Random(Avalanche(seed, salt));
        return min + ((float)random.NextDouble() * (max - min));
    }

    // Same avalanche as RangeFor, for picking one of `count` discrete options (a hairstyle)
    // rather than a continuous value.
    public static int IndexFor(int seed, int salt, int count)
    {
        var random = new Random(Avalanche(seed, salt));
        return random.Next(count);
    }

    private static int Avalanche(int seed, int salt) =>
        SeedHash.Avalanche(unchecked(((uint)seed * 0x9E3779B1u) + (uint)salt));
}
