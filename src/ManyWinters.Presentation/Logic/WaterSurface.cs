using Godot;

namespace ManyWinters.Presentation.Logic;

// Where water lies and how the ground gives way under it. Standing water is level and a river
// only ever runs downhill, while the ground carries a bump and the DEM's own unevenness: left
// alone, the ground pokes up through any flat surface laid over it. So each water body gets a
// level, and the ground under and beside it is held below that level.
internal static class WaterSurface
{
    // How deep the bed lies once ShelfMeters in from the edge; shallower toward the shore.
    private const float Depth = 0.5f;
    private const float ShelfMeters = 3f;

    // How steeply the ground may rise away from the water's edge, so a bump right at the
    // shore cannot stand up over the water's edge.
    private const float BankSlope = 0.5f;

    // Every number that changes the carved ground, for the terrain mesh cache key.
    internal static string Fingerprint => string.Join('|', Depth, ShelfMeters, BankSlope);

    // The level of each connected body of water (vertices with a positive inside distance, 8-way
    // connected), at its lowest ground: the DEM samples a lake or river at its surface, so its
    // lowest point is the nearest to the real level. Vertices within reachCells outside a body
    // take its level too, since triangles across the edge and the bank beside it need one.
    // NaN everywhere else.
    internal static float[,] AreaLevels(float[,] insideDistance, Func<int, int, float> ground, int reachCells)
    {
        var size = insideDistance.GetLength(0);
        var levels = new float[size, size];
        var body = new int[size, size];
        var bodyLevels = new List<float>();

        for (var row = 0; row < size; row++)
        {
            for (var col = 0; col < size; col++)
            {
                levels[row, col] = float.NaN;
                body[row, col] = -1;
            }
        }

        for (var row = 0; row < size; row++)
        {
            for (var col = 0; col < size; col++)
            {
                if (insideDistance[row, col] <= 0f || body[row, col] >= 0)
                {
                    continue;
                }

                var id = bodyLevels.Count;
                var members = new List<(int Row, int Col)>();
                var pending = new Stack<(int Row, int Col)>();
                pending.Push((row, col));
                body[row, col] = id;
                var lowest = float.PositiveInfinity;
                while (pending.TryPop(out var at))
                {
                    members.Add(at);
                    lowest = Math.Min(lowest, ground(at.Row, at.Col));
                    foreach (var (r, c) in Around(at.Row, at.Col, 1, size))
                    {
                        if (insideDistance[r, c] > 0f && body[r, c] < 0)
                        {
                            body[r, c] = id;
                            pending.Push((r, c));
                        }
                    }
                }

                bodyLevels.Add(lowest);
                foreach (var (r, c) in members)
                {
                    levels[r, c] = lowest;
                }
            }
        }

        for (var row = 0; row < size; row++)
        {
            for (var col = 0; col < size; col++)
            {
                if (body[row, col] >= 0)
                {
                    continue;
                }

                foreach (var (r, c) in Around(row, col, reachCells, size))
                {
                    if (body[r, c] >= 0)
                    {
                        levels[row, col] = bodyLevels[body[r, c]];
                        break;
                    }
                }
            }
        }

        return levels;
    }

    // A river's surface along its samples, never rising toward its mouth. Mapped waterways run
    // in the direction of flow, but whichever end lies lower is taken as the mouth, so a way
    // drawn against the flow still runs downhill rather than sinking into a flat trench.
    internal static float[] Descending(IReadOnlyList<float> heights)
    {
        var profile = heights.ToArray();
        if (profile.Length == 0)
        {
            return profile;
        }

        if (profile[0] >= profile[^1])
        {
            for (var i = 1; i < profile.Length; i++)
            {
                profile[i] = Math.Min(profile[i], profile[i - 1]);
            }
        }
        else
        {
            for (var i = profile.Length - 2; i >= 0; i--)
            {
                profile[i] = Math.Min(profile[i], profile[i + 1]);
            }
        }

        return profile;
    }

    // The highest the ground may stand at a point insideDistance in from a water edge (negative
    // outside it), given the water's level there: under the water a shelf deepening to Depth,
    // outside a bank rising at BankSlope. Beyond reach outside, the water sets no limit.
    internal static float Ceiling(float level, float insideDistance, float reach)
    {
        if (insideDistance >= 0f)
        {
            return level - (Depth * Math.Clamp(insideDistance / ShelfMeters, 0f, 1f));
        }

        return -insideDistance >= reach ? float.PositiveInfinity : level + (BankSlope * -insideDistance);
    }

    // Distance from p to the segment a-b, and how far along it (0 at a, 1 at b) the nearest
    // point lies.
    internal static (float Distance, float Along) ToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var lengthSquared = ab.LengthSquared();
        var along = lengthSquared == 0f ? 0f : Math.Clamp((p - a).Dot(ab) / lengthSquared, 0f, 1f);
        return (p.DistanceTo(a + (ab * along)), along);
    }

    private static IEnumerable<(int Row, int Col)> Around(int row, int col, int cells, int size)
    {
        for (var r = Math.Max(0, row - cells); r <= Math.Min(size - 1, row + cells); r++)
        {
            for (var c = Math.Max(0, col - cells); c <= Math.Min(size - 1, col + cells); c++)
            {
                if (r != row || c != col)
                {
                    yield return (r, c);
                }
            }
        }
    }
}
