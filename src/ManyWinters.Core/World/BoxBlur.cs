namespace ManyWinters.Core.World;

// Separable box blur over a square grid (horizontal pass, then vertical) - what the
// presentation layer softens the fog-of-war boundary with, so a fog edge fades rather than
// cutting across the ground as a hard line. Separable means two 1D passes instead of one 2D
// kernel: the same result for a box kernel, at a fraction of the samples.
//
// Samples past the grid's own edge clamp to the nearest real cell rather than wrapping, which
// matches how the fog texture is sampled (repeat disabled) - wrapping would bleed the far side
// of the map into this one.
public static class BoxBlur
{
    // The grid is square and its size comes from the array itself rather than a parameter
    // alongside it: a size that disagreed with the array it describes is a bug with nowhere
    // useful to fail.
    public static float[,] Blur(float[,] source, int radius)
    {
        var size = source.GetLength(0);
        var window = (2 * radius) + 1;

        var horizontal = new float[size, size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var sum = 0f;
                for (var dx = -radius; dx <= radius; dx++)
                {
                    // Stryker disable once Arithmetic: dx runs symmetrically, so subtracting samples the same window
                    sum += source[y, Math.Clamp(x + dx, 0, size - 1)];
                }

                horizontal[y, x] = sum / window;
            }
        }

        var result = new float[size, size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var sum = 0f;
                for (var dy = -radius; dy <= radius; dy++)
                {
                    // Stryker disable once Arithmetic: dy runs symmetrically, as dx above
                    sum += horizontal[Math.Clamp(y + dy, 0, size - 1), x];
                }

                result[y, x] = sum / window;
            }
        }

        return result;
    }
}
