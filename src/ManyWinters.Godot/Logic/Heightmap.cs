using Godot;
using ManyWinters.Core.Maps;

namespace ManyWinters.Godot.Logic;

// The shape of the ground: a real elevation grid, the light procedural bump laid over it, and
// the finer grid the mesh is actually built on. Everything here is a function of the numbers
// handed in at construction - loading them out of JSON, and turning the answers into vertices,
// is TerrainRenderer's half.
//
// The subdivision and bump tuning live here rather than with the renderer because they decide
// the terrain's own shape, not how it looks once built.
internal sealed class Heightmap
{
    // The real elevation data is only a 41x41 grid at 25m spacing (docs/terrain-and-world-
    // scale-architecture.md) - bilinear interpolation between those samples alone reads as
    // smooth, blank ground. A light procedural bump on top brings back some fine, organic
    // surface variation: amplitude deliberately small (a texture, not a new hill range). The
    // wavelength has to stay meaningfully larger than however far apart the mesh's own
    // vertices are, or it aliases into per-vertex jitter instead of smooth rolling - see
    // FineGridSize for why that is a mesh-resolution limit and not a performance one, since
    // evaluating the noise costs the same at any wavelength.
    private const int BumpNoiseSeed = 42;
    private const int BumpOctaves = 3;
    private const double BumpFrequency = 1.0 / 10.0;
    private const float BumpAmplitudeMeters = 2.0f;

    // Each 25m source cell is rendered as this many smaller ones: 10 gives 2.5m between mesh
    // vertices, four samples per 10m bump wave - a comfortable margin above the two-sample
    // Nyquist minimum rather than right at the edge of aliasing - while the DEM's own samples
    // stay exactly as they were. The real cost is vertex count: (41-1)*10+1 = 401 per side
    // instead of 41, built once at load, which is the actual reason not to push this higher
    // just to allow a shorter wavelength.
    private const int SubdivisionsPerCell = 10;

    private static readonly Noise2D BumpNoise = new(BumpNoiseSeed);

    private readonly float[][] _heights;
    private readonly int _gridSize;
    private readonly float _cellSizeMeters;

    internal Heightmap(float[][] heights, int gridSize, float cellSizeMeters)
    {
        _heights = heights;
        _gridSize = gridSize;
        _cellSizeMeters = cellSizeMeters;

        var min = float.MaxValue;
        var max = float.MinValue;
        foreach (var row in heights)
        {
            foreach (var height in row)
            {
                min = Math.Min(min, height);
                max = Math.Max(max, height);
            }
        }

        MinHeight = min;
        MaxHeight = max;
        HalfExtentMeters = (gridSize - 1) * cellSizeMeters / 2f;
    }

    // Lowest and highest elevation in the source data. The ground is shifted so the lowest
    // point sits at zero, and the span between them is what the renderer colours by.
    internal float MinHeight { get; }

    internal float MaxHeight { get; }

    // Half the map's width, so local coordinates run from -this to +this on both axes.
    internal float HalfExtentMeters { get; }

    internal int FineGridSize => ((_gridSize - 1) * SubdivisionsPerCell) + 1;

    internal float FineCellSize => _cellSizeMeters / SubdivisionsPerCell;

    // The real elevation alone at any local point, with no bump - bilinear between the DEM's
    // own samples, clamped to the grid's edge outside it. A river surface uses this rather
    // than the bumped height: water really is smoother than the ground around it, and the
    // bump would make it look choppy.
    internal float RawAt(float x, float z) =>
        Bilinear(_gridSize, _cellSizeMeters, x, z, (row, col) => _heights[row][col]) - MinHeight;

    // Elevation plus bump at one exact fine-grid vertex - what the mesh's own vertices are,
    // and what HeightAt blends between for anywhere else.
    internal float FineVertexAt(int row, int col)
    {
        var x = (col * FineCellSize) - HalfExtentMeters;
        var z = (row * FineCellSize) - HalfExtentMeters;

        return RawAt(x, z) + BumpAt(x, z);
    }

    // The ground height anything standing on the terrain should use. Deliberately blended
    // between the *mesh's own vertices* rather than recomputed from the formula they were
    // built from: the two agree only if this interpolates exactly what was rendered, and a
    // gap between them shows up as a person floating above, or sinking into, ground drawn
    // flat right beneath them.
    internal float HeightAt(float x, float z) =>
        Bilinear(FineGridSize, FineCellSize, x, z, FineVertexAt);

    // Every number here that changes the ground's own shape, in one string, so a mesh cached
    // under different tuning can never be served as though it still matched. A constant added
    // above belongs in here too - which is the point of putting it next to them.
    internal static string ShapeFingerprint =>
        string.Join('|', SubdivisionsPerCell, BumpNoiseSeed, BumpOctaves, BumpFrequency, BumpAmplitudeMeters);

    // Fbm's own [0, 1] range remapped to [-1, 1] first - otherwise every point would only be
    // nudged upward, raising the whole terrain by roughly half the amplitude instead of
    // rolling both up and down around the real elevation. Exposed because the mesh build wants
    // the raw sample and the bump separately, to avoid paying for the raw one twice.
    internal static float BumpAt(float x, float z) =>
        (float)((BumpNoise.Fbm(x, z, BumpOctaves, BumpFrequency) - 0.5) * 2.0) * BumpAmplitudeMeters;

    // One bilinear sample over a square grid of `size` vertices spaced `spacing` apart and
    // centred on the origin, clamped to the edge outside it. Shared by both grids so the
    // coarse and the fine sample can never drift into two slightly different formulas.
    private float Bilinear(int size, float spacing, float x, float z, Func<int, int, float> at)
    {
        var colF = Mathf.Clamp((x + HalfExtentMeters) / spacing, 0, size - 1);
        var rowF = Mathf.Clamp((z + HalfExtentMeters) / spacing, 0, size - 1);
        var col0 = (int)Mathf.Floor(colF);
        var row0 = (int)Mathf.Floor(rowF);
        var col1 = Math.Min(col0 + 1, size - 1);
        var row1 = Math.Min(row0 + 1, size - 1);

        var top = Mathf.Lerp(at(row0, col0), at(row0, col1), colF - col0);
        var bottom = Mathf.Lerp(at(row1, col0), at(row1, col1), colF - col0);

        return Mathf.Lerp(top, bottom, rowF - row0);
    }
}
