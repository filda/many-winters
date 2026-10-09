namespace ManyWinters.Presentation.Logic;

// Signed distance to a feature's edge over the terrain's vertex grid, positive inside, from
// which a shader draws the edge smooth at any zoom rather than along the grid's cells. Exact
// only in a band of vertices near the edge, bandCells wide on each side: an exact distance
// walks every edge of every ring, and further out any value of the right sign draws the same.
// The band is wider than one cell so the shader can push the edge out or in a little and still
// find true distances where it lands.
internal static class SignedDistanceBand
{
    internal static float[,] Build(int size, Func<int, int, bool> inside, Func<int, int, double> signedDistance, int bandCells, float far)
    {
        var isInside = new bool[size, size];
        for (var row = 0; row < size; row++)
        {
            for (var col = 0; col < size; col++)
            {
                isInside[row, col] = inside(row, col);
            }
        }

        bool NearTheEdge(int row, int col)
        {
            for (var dRow = -bandCells; dRow <= bandCells; dRow++)
            {
                for (var dCol = -bandCells; dCol <= bandCells; dCol++)
                {
                    var r = row + dRow;
                    var c = col + dCol;
                    if (r >= 0 && r < size && c >= 0 && c < size && isInside[r, c] != isInside[row, col])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        var distances = new float[size, size];
        for (var row = 0; row < size; row++)
        {
            for (var col = 0; col < size; col++)
            {
                distances[row, col] = NearTheEdge(row, col)
                    ? Math.Clamp((float)signedDistance(row, col), -far, far)
                    : isInside[row, col] ? far : -far;
            }
        }

        return distances;
    }

    // A triangle is drawn when any corner lies inside the feature or within reach of its edge,
    // reach being how far the shader may push the edge outward. Further out the edge cannot
    // cross it, unless a sliver of the feature narrower than a cell slips between its corners.
    internal static bool Touches(float a, float b, float c, float reach) => a > -reach || b > -reach || c > -reach;
}
