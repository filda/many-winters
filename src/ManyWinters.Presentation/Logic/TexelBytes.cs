using System.Runtime.InteropServices;

namespace ManyWinters.Presentation.Logic;

// Grids of [row, column] values laid out as the raw bytes an Image of the matching format is
// created from in one call - a SetPixel per texel is a call into the engine each.
internal static class TexelBytes
{
    // Four equally sized grids, one per channel, as Rgba8: each value 0..1 rounded to a byte the
    // way Image.SetPixel rounds it, anything outside clamped.
    public static byte[] Rgba8(float[,] r, float[,] g, float[,] b, float[,] a)
    {
        var rows = r.GetLength(0);
        var columns = r.GetLength(1);
        var bytes = new byte[rows * columns * 4];
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var offset = ((row * columns) + column) * 4;
                bytes[offset] = ToByte(r[row, column]);
                bytes[offset + 1] = ToByte(g[row, column]);
                bytes[offset + 2] = ToByte(b[row, column]);
                bytes[offset + 3] = ToByte(a[row, column]);
            }
        }

        return bytes;
    }

    // One grid as Rf, every value multiplied by scale on the way.
    public static byte[] Rf(float[,] values, float scale)
    {
        var rows = values.GetLength(0);
        var columns = values.GetLength(1);
        var floats = new float[rows * columns];
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                floats[(row * columns) + column] = values[row, column] * scale;
            }
        }

        return MemoryMarshal.AsBytes(floats.AsSpan()).ToArray();
    }

    private static byte ToByte(float channel) => (byte)Math.Clamp((int)MathF.Round(channel * 255f), 0, 255);
}
