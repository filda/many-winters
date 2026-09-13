using Godot;
using ManyWinters.Core.Maps;

namespace ManyWinters.Godot.Logic;

// The shape of the ground: the real elevation grid, a light procedural bump on top, and the
// finer grid the mesh is built on. Pure arithmetic over the numbers handed in at construction;
// loading them and building vertices is TerrainRenderer's half. The subdivision and bump
// tuning live here because they decide the terrain's shape, not how it looks once built.
internal sealed class Heightmap
{
    // The DEM is a 41x41 grid at 25m spacing (docs/terrain-and-world-scale-architecture.md);
    // bilinear interpolation alone reads as blank ground, so a small bump is laid on top as
    // texture, not new hills. Its wavelength must stay well above the fine grid's vertex
    // spacing or it aliases into per-vertex jitter (see SubdivisionsPerCell).
    private const int BumpNoiseSeed = 42;
    private const int BumpOctaves = 3;
    private const double BumpFrequency = 1.0 / 10.0;
    private const float BumpAmplitudeMeters = 2.0f;

    // Each 25m source cell becomes this many mesh cells: 10 gives 2.5m vertex spacing, four
    // samples per 10m bump wave (double the Nyquist minimum), with the DEM's own samples kept
    // exact. The cost is vertex count, (41-1)*10+1 = 401 per side, built once at load.
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

    // Lowest and highest source elevation. The ground is shifted so the lowest sits at zero;
    // the renderer colours by the span between them.
    internal float MinHeight { get; }

    internal float MaxHeight { get; }

    // Half the map's width: local coordinates run from -this to +this on both axes.
    internal float HalfExtentMeters { get; }

    internal int FineGridSize => ((_gridSize - 1) * SubdivisionsPerCell) + 1;

    internal float FineCellSize => _cellSizeMeters / SubdivisionsPerCell;

    // Elevation without the bump: bilinear between the DEM's samples, clamped at the grid
    // edge. Water surfaces use this - the bump would make them look choppy.
    internal float RawAt(float x, float z) =>
        Bilinear(_gridSize, _cellSizeMeters, x, z, (row, col) => _heights[row][col]) - MinHeight;

    // Elevation plus bump at one fine-grid vertex - what the mesh's vertices are, and what
    // HeightAt blends between.
    internal float FineVertexAt(int row, int col)
    {
        var x = (col * FineCellSize) - HalfExtentMeters;
        var z = (row * FineCellSize) - HalfExtentMeters;

        return RawAt(x, z) + BumpAt(x, z);
    }

    // Ground height for anything standing on the terrain. Blends between the mesh's own
    // vertices rather than re-evaluating the formula, so it matches what was rendered - a gap
    // shows as a person floating above or sinking into the ground.
    internal float HeightAt(float x, float z) =>
        Bilinear(FineGridSize, FineCellSize, x, z, FineVertexAt);

    // Every number that changes the ground's shape, so a mesh cached under different tuning
    // is never served as matching. Any constant added above belongs in here too.
    internal static string ShapeFingerprint =>
        string.Join('|', SubdivisionsPerCell, BumpNoiseSeed, BumpOctaves, BumpFrequency, BumpAmplitudeMeters);

    // Fbm's [0, 1] remapped to [-1, 1] so the bump rolls both ways around the real elevation
    // instead of raising the whole terrain by half the amplitude. Exposed because the mesh
    // build wants the raw sample and the bump separately.
    internal static float BumpAt(float x, float z) =>
        (float)((BumpNoise.Fbm(x, z, BumpOctaves, BumpFrequency) - 0.5) * 2.0) * BumpAmplitudeMeters;

    // Bilinear sample over a square grid of `size` vertices, `spacing` apart, centred on the
    // origin, clamped at the edge. Shared by both grids so they cannot drift into two formulas.
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
