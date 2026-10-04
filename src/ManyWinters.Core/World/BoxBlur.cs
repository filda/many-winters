namespace ManyWinters.Core.World;

// Separable box blur over a square grid (horizontal pass, then vertical) - the presentation
// layer softens the fog-of-war edge with it. Samples past the edge clamp to the nearest cell
// rather than wrapping, matching a fog texture sampled with repeat disabled.
//
// Each pass slides a running sum along the line - one sample in, one out - so a cell costs
// the same whatever the radius. The fog is rebuilt every tick, and re-adding the whole window
// for every cell made this the largest share of it. The sum is a double so sliding it across
// a whole line accumulates no visible drift.
public static class BoxBlur
{
    // Square grid; the size comes from the array itself, so it can never disagree with it.
    public static float[,] Blur(float[,] source, int radius)
    {
        var size = source.GetLength(0);
        var horizontal = new float[size, size];
        var result = new float[size, size];

        for (var line = 0; line < size; line++)
        {
            BlurLine(source, horizontal, line, alongRow: true, radius);
        }

        for (var line = 0; line < size; line++)
        {
            BlurLine(horizontal, result, line, alongRow: false, radius);
        }

        return result;
    }

    // One row (alongRow) or one column of `from`, blurred into the same line of `to`.
    private static void BlurLine(float[,] from, float[,] to, int line, bool alongRow, int radius)
    {
        var size = from.GetLength(0);
        var last = size - 1;
        var window = (2 * radius) + 1;

        float Sample(int i)
        {
            var clamped = Math.Clamp(i, 0, last);
            return alongRow ? from[line, clamped] : from[clamped, line];
        }

        var sum = 0.0;
        for (var offset = -radius; offset <= radius; offset++)
        {
            sum += Sample(offset);
        }

        for (var i = 0; i < size; i++)
        {
            var value = (float)(sum / window);
            if (alongRow)
            {
                to[line, i] = value;
            }
            else
            {
                to[i, line] = value;
            }

            sum += Sample(i + radius + 1) - Sample(i - radius);
        }
    }
}
