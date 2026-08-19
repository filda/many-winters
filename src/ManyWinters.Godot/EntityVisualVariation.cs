using Godot;

namespace ManyWinters.Godot;

// Deterministic per-instance variety: same seed (an entity's stable id) always produces the same
// tint/rotation, so repeated saves/reloads don't reshuffle how things look, but instances of the
// same kind don't render as identical clones either.
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

    public static float RotationDegrees(int seed, float maxDegrees)
    {
        var random = new Random(seed);
        return ((float)random.NextDouble() - 0.5f) * 2f * maxDegrees;
    }
}
