namespace ManyWinters.Godot.Logic;

// The square bitmap the fog-of-war shaders sample, and the two-way mapping between a texel and
// a world coordinate. Both directions live together because they must be exact inverses and do
// not look it inline: out to the world adds half a texel to land on the centre, back adds
// nothing and lets the floor recover the index. One wrong and the fog sits half a texel off.
internal readonly record struct TexelGrid(int Size, float HalfExtentMeters)
{
    // One texel per exploration cell, rounded up so the bitmap covers the whole map.
    internal static TexelGrid Covering(float halfExtentMeters, float cellSizeMeters) =>
        new((int)MathF.Ceiling((2f * halfExtentMeters) / cellSizeMeters), halfExtentMeters);

    internal float ExtentMeters => 2f * HalfExtentMeters;

    // How much ground one texel stands for - the scale the distance field is reported in.
    internal float MetresPerTexel => ExtentMeters / Size;

    // The world coordinate at the *centre* of a texel, where exploration state is sampled: a
    // texel is an area, and its corner would bias every sample half a texel toward the origin.
    internal float WorldAt(int texel) => (((texel + 0.5f) / Size) - 0.5f) * ExtentMeters;

    // Which texel a world coordinate falls in. Clamped, not wrapped or refused: callers ask
    // about spots slightly outside the map (a cloud candidate near the edge).
    internal int TexelAt(float world) =>
        Math.Clamp((int)MathF.Floor(((world / ExtentMeters) + 0.5f) * Size), 0, Size - 1);
}
