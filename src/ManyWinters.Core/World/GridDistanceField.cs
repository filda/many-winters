namespace ManyWinters.Core.World;

// Distance from every cell of a grid to the nearest cell flagged true, in cell units - what
// the presentation layer needs to fade the unknown tier of fog-of-war out with distance from
// anything the group has ever explored (concentric: party, visible ground, parchment, then
// darkness), rather than with distance from the map's own centre or edge. A two-pass chamfer
// transform (forward then backward sweep over the 8-neighbourhood, weights 1 and sqrt 2):
// linear in cell count, so cheap enough to redo on every fog rebuild, and within a few
// percent of the true Euclidean distance - more than accurate enough to drive a soft fade.
public static class GridDistanceField
{
    // Returned for every cell when nothing in the grid is flagged - finite (not
    // PositiveInfinity) so it survives being written into a float texture and compared with
    // ordinary smoothstep-style thresholds without special-casing.
    public const float Unreachable = 1e6f;

    private static readonly float Diagonal = MathF.Sqrt(2f);

    public static float[,] DistanceToNearestTrue(bool[,] targets)
    {
        var rows = targets.GetLength(0);
        var cols = targets.GetLength(1);
        var distance = new float[rows, cols];

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < cols; x++)
            {
                distance[y, x] = targets[y, x] ? 0f : Unreachable;
            }
        }

        // Forward sweep: each cell may be reached from its already-visited neighbours above
        // and to the left.
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < cols; x++)
            {
                var best = distance[y, x];
                if (x > 0) best = MathF.Min(best, distance[y, x - 1] + 1f);
                if (y > 0) best = MathF.Min(best, distance[y - 1, x] + 1f);
                if (x > 0 && y > 0) best = MathF.Min(best, distance[y - 1, x - 1] + Diagonal);
                if (x < cols - 1 && y > 0) best = MathF.Min(best, distance[y - 1, x + 1] + Diagonal);
                distance[y, x] = best;
            }
        }

        // Backward sweep: the mirror image, from the neighbours below and to the right.
        for (var y = rows - 1; y >= 0; y--)
        {
            for (var x = cols - 1; x >= 0; x--)
            {
                var best = distance[y, x];
                if (x < cols - 1) best = MathF.Min(best, distance[y, x + 1] + 1f);
                if (y < rows - 1) best = MathF.Min(best, distance[y + 1, x] + 1f);
                if (x < cols - 1 && y < rows - 1) best = MathF.Min(best, distance[y + 1, x + 1] + Diagonal);
                if (x > 0 && y < rows - 1) best = MathF.Min(best, distance[y + 1, x - 1] + Diagonal);
                distance[y, x] = best;
            }
        }

        return distance;
    }
}
