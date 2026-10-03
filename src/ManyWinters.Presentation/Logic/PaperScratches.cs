namespace ManyWinters.Presentation.Logic;

// The scratched hatching under a page's stains, drawn the way the title page's plates are
// (art/generate_splash.py): parallel diagonal strokes, each nudged sideways by its own amount,
// bending on its own slow wave, its own thickness and pressure, and broken where the pen lifted.
// A ruled grid of identical lines reads as a screen print; this reads as a hand.
//
// Everything repeats over Tile in both directions, so the tile lays edge to edge without a seam:
// the stroke slots are cut from the diagonal wrapped to the tile, the wave fits a whole number of
// times into it, and so do the pen-lift segments.
internal static class PaperScratches
{
    // A multiple of every spacing a page can come out with (and of each one plus one, for the
    // crossing course), of the segment length and of the patch size.
    internal const int Tile = 840;

    private const int SegmentLength = 14;

    // How often the pen lifts: this share of segments is left out.
    private const float PenLift = 0.14f;

    // The stroke's thickness before each line's own jitter, in pixels. Thin, because this lies
    // under text rather than shading a mountain.
    private const float StrokeWidth = 1.1f;

    // The crossing course: the patches it is laid in, the share of them it is laid in, and how
    // much lighter it is pressed than the first.
    private const int PatchSize = 60;
    private const float CrossedShare = 0.5f;
    private const float CrossPressure = 0.6f;
    private const int CrossSalt = 100;

    // The tile as RGBA8 bytes, row by row: white wherever there is ink, its alpha the pressure of
    // the stroke there, and clear everywhere else. The caller colours it.
    internal static byte[] Rgba(int spacing, bool rising, int salt)
    {
        var data = new byte[Tile * Tile * 4];
        for (var y = 0; y < Tile; y++)
        {
            for (var x = 0; x < Tile; x++)
            {
                var index = ((y * Tile) + x) * 4;
                data[index] = 255;
                data[index + 1] = 255;
                data[index + 2] = 255;
                data[index + 3] = (byte)(InkAt(x, y, spacing, rising, salt) * 255f);
            }
        }

        return data;
    }

    // How hard the pen pressed at this pixel: zero off every stroke. A second, lighter course of
    // strokes leans the other way and crosses the first only in patches, as the title page's
    // plates cross their hatching where the tone deepens; where the two meet, the heavier wins.
    internal static float InkAt(int x, int y, int spacing, bool rising, int salt)
    {
        // Across the strokes and along them; which is which decides the way they lean.
        var across = Wrap(rising ? x + y : x - y);
        var along = Wrap(rising ? x - y : x + y);

        var stroke = Stroke(across, along, spacing, salt);
        if (Hash((across / PatchSize * 7L) + (along / PatchSize), salt + CrossSalt) <= CrossedShare)
        {
            return stroke;
        }

        // The crossing course runs along what the first runs across, a little wider apart, so the
        // two never fall into step.
        return MathF.Max(stroke, CrossPressure * Stroke(along, across, spacing + 1, salt + CrossSalt));
    }

    private static float Stroke(int across, int along, int spacing, int salt)
    {
        var line = across / spacing;
        var lateral = (Hash(line, salt) - 0.5f) * 2.2f;
        var waves = 1 + (int)(Hash(line, salt + 1) * 3f);
        var phase = Hash(line, salt + 2) * 2f * MathF.PI;
        var bend = 1.5f + (Hash(line, salt + 3) * 2.5f);
        var thickness = StrokeWidth * (0.55f + (Hash(line, salt + 4) * 0.9f));

        var curve = MathF.Sin((along * waves * 2f * MathF.PI / Tile) + phase) * bend;
        var offset = Modulo(across - lateral - curve, spacing);
        if (offset >= thickness)
        {
            return 0f;
        }

        var segment = (line * 100003L) + (along / SegmentLength);
        if (Hash(segment, salt + 5) <= PenLift)
        {
            return 0f;
        }

        return 0.5f + (Hash(line, salt + 6) * 0.5f);
    }

    private static int Wrap(int value) => ((value % Tile) + Tile) % Tile;

    private static float Modulo(float value, float divisor) => ((value % divisor) + divisor) % divisor;

    // The title page's own line hash, so a stroke's character is fixed by which stroke it is.
    private static float Hash(long index, int salt)
    {
        var h = unchecked((index * 2654435761L) + (salt * 40503L)) & 0xFFFFFFFFL;
        h = (h ^ (h >> 13)) & 0xFFFFFFFFL;
        return h % 10007 / 10007f;
    }
}
