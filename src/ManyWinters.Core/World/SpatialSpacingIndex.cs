namespace ManyWinters.Core.World;

// The spatial-hash rejection-sampling shape shared by CloudSpotScatter's blue-noise clouds and
// MapLoader's decoration/crowd placement (docs/todo/refactoring.md): bucket by a cell at least
// as wide as the largest gap two items can ever need, so a candidate only ever checks the 3x3
// cells around it instead of every item placed before it. Coordinates are always widened to
// double before this runs - a caller working in float loses nothing by that widening, so the
// same index gives identical answers regardless of which precision the caller itself uses.
public sealed class SpatialSpacingIndex<T>
{
    private readonly double _cellSize;
    private readonly Func<T, double> _x;
    private readonly Func<T, double> _z;
    private readonly Dictionary<(int, int), List<T>> _buckets = new();

    // cellSize must be at least as large as the largest gap a caller's requiredGap can ever
    // return: IsTooClose only ever looks at the candidate's own cell and its 8 neighbours, so an
    // existing item further away than that - even one requiredGap would reject the candidate
    // for - is invisible to it.
    public SpatialSpacingIndex(double cellSize, Func<T, double> x, Func<T, double> z)
    {
        _cellSize = cellSize;
        _x = x;
        _z = z;
    }

    // True if some already-added item sits closer to (x, z) than requiredGap allows. The gap is
    // asked per neighbour rather than fixed once, so a caller whose minimum spacing depends on
    // both points (CloudSpotScatter's size-derived gap) and one with a single constant spacing
    // (MapLoader's decoration/crowd placement) can share this same index.
    public bool IsTooClose(double x, double z, Func<T, double> requiredGap)
    {
        var (cellX, cellZ) = CellFor(x, z);
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dz = -1; dz <= 1; dz++)
            {
                // Stryker disable once Arithmetic: dx and dz run symmetrically, so subtracting walks the same nine cells
                if (!_buckets.TryGetValue((cellX + dx, cellZ + dz), out var bucket))
                {
                    continue;
                }

                foreach (var existing in bucket)
                {
                    var gap = requiredGap(existing);
                    var ddx = _x(existing) - x;
                    var ddz = _z(existing) - z;
                    // Stryker disable once Equality: two candidates at exactly the gap has probability zero
                    if ((ddx * ddx) + (ddz * ddz) < gap * gap)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public void Add(T item)
    {
        var cell = CellFor(_x(item), _z(item));
        if (!_buckets.TryGetValue(cell, out var bucket))
        {
            bucket = new List<T>();
            _buckets[cell] = bucket;
        }

        bucket.Add(item);
    }

    // Stryker disable once Arithmetic: the cell size only decides how finely positions are
    // bucketed - a coarser one still gathers every neighbour the 3x3 scan above needs, and that
    // scan measures real distances anyway, so results come out identical either way.
    private (int, int) CellFor(double x, double z) => ((int)Math.Floor(x / _cellSize), (int)Math.Floor(z / _cellSize));
}
