using ManyWinters.Presentation.Logic;

namespace ManyWinters.Presentation.Tests;

public class TexelBytesTests
{
    private static readonly float[] HalvedInRowOrder = [0.5f, 1f, 1.5f, 2f];

    [Fact]
    public void Rgba8InterleavesTheFourGridsRowByRow()
    {
        float[,] r = { { 0f, 1f } };
        float[,] g = { { 1f, 0f } };
        float[,] b = { { 0f, 0f } };
        float[,] a = { { 1f, 1f } };

        Assert.Equal(new byte[] { 0, 255, 0, 255, 255, 0, 0, 255 }, TexelBytes.Rgba8(r, g, b, a));
    }

    [Fact]
    public void Rgba8PlacesTheSecondRowAfterTheWholeFirstRow()
    {
        float[,] r = { { 0f, 0f }, { 1f, 0f } };
        var zero = new float[2, 2];

        var bytes = TexelBytes.Rgba8(r, zero, zero, zero);

        Assert.Equal(16, bytes.Length);
        Assert.Equal(255, bytes[8]);
        Assert.Equal(0, bytes[4]);
    }

    [Fact]
    public void Rgba8RoundsToTheNearestByte()
    {
        float[,] half = { { 0.5f } };
        float[,] low = { { 0.1f } };
        var zero = new float[1, 1];

        Assert.Equal(new byte[] { 128, 26, 0, 0 }, TexelBytes.Rgba8(half, low, zero, zero));
    }

    [Fact]
    public void Rgba8ClampsValuesOutsideZeroToOne()
    {
        float[,] over = { { 1.5f } };
        float[,] under = { { -0.5f } };
        var zero = new float[1, 1];

        Assert.Equal(new byte[] { 255, 0, 0, 0 }, TexelBytes.Rgba8(over, under, zero, zero));
    }

    [Fact]
    public void RfWritesEachScaledValueAsAFloatInRowOrder()
    {
        float[,] values = { { 1f, 2f }, { 3f, 4f } };

        var bytes = TexelBytes.Rf(values, 0.5f);

        var floats = new float[4];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        Assert.Equal(HalvedInRowOrder, floats);
    }
}
