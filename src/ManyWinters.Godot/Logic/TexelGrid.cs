namespace ManyWinters.Godot.Logic;

// The square bitmap the fog-of-war shaders sample, and the two-way mapping between one of its
// texels and a world coordinate. Both directions live here together because they have to stay
// exact inverses and, written out inline, they do not look like it: going out to the world adds
// half a texel to land on a texel's centre, while coming back adds nothing and lets the floor
// recover the index. Get one of the two wrong and the fog sits half a texel off the ground it
// describes - a boundary that looks almost right, on ground nobody can point at.
internal readonly record struct TexelGrid(int Size, float HalfExtentMeters)
{
    // One texel per exploration cell, rounded up so the bitmap always covers the whole map
    // rather than stopping just short of its far edge.
    internal static TexelGrid Covering(float halfExtentMeters, float cellSizeMeters) =>
        new((int)MathF.Ceiling((2f * halfExtentMeters) / cellSizeMeters), halfExtentMeters);

    internal float ExtentMeters => 2f * HalfExtentMeters;

    // How much ground one texel stands for - the scale the distance field is reported in.
    internal float MetresPerTexel => ExtentMeters / Size;

    // The world coordinate at the *centre* of a texel, which is where the exploration state is
    // sampled: a texel is an area, and its corner would bias every sample half a texel toward
    // the map's origin.
    internal float WorldAt(int texel) => (((texel + 0.5f) / Size) - 0.5f) * ExtentMeters;

    // Which texel a world coordinate falls in. Clamped rather than wrapped or refused: callers
    // ask about spots that can sit slightly outside the map (a cloud candidate near the edge),
    // and the nearest edge texel is the honest answer there.
    internal int TexelAt(float world) =>
        Math.Clamp((int)MathF.Floor(((world / ExtentMeters) + 0.5f) * Size), 0, Size - 1);
}
