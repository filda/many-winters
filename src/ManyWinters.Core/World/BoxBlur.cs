namespace ManyWinters.Core.World;

// Separable box blur over a square grid (horizontal pass, then vertical) - the presentation
// layer softens the fog-of-war edge with it. Samples past the edge clamp to the nearest cell
// rather than wrapping, matching a fog texture sampled with repeat disabled.
public static class BoxBlur
{
    // Square grid; the size comes from the array itself, so it can never disagree with it.
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
