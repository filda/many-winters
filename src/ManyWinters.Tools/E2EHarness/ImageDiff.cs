using System.Drawing;
using System.Runtime.Versioning;

namespace ManyWinters.Tools.E2EHarness;

[SupportedOSPlatform("windows")]
public sealed record ImageDiffResult(bool Matches, double DifferentPixelFraction, Bitmap? DiffImage);

/// <summary>
/// Compares a captured screenshot against a checked-in baseline PNG with a tolerance, since
/// font/antialiasing rendering is not bit-identical run to run. On mismatch, callers should save
/// <see cref="ImageDiffResult.DiffImage"/> next to the other Cake task artifacts (see
/// BuildContext.ArtifactsDirectory) for a human to look at, the same way InspectCode's report is.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ImageDiff
{
    // Per-channel tolerance before a pixel counts as different at all - antialiasing and font
    // hinting shift individual pixel values slightly even when nothing behavioral changed.
    private const int PerChannelTolerance = 24;

    public static ImageDiffResult Compare(Bitmap actual, Bitmap baseline, double toleranceFraction)
    {
        if (actual.Width != baseline.Width || actual.Height != baseline.Height)
        {
            return new ImageDiffResult(false, 1.0, null);
        }

        var width = actual.Width;
        var height = actual.Height;
        var diff = new Bitmap(width, height);
        var differentPixels = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var a = actual.GetPixel(x, y);
                var b = baseline.GetPixel(x, y);
                var isDifferent = Math.Abs(a.R - b.R) > PerChannelTolerance
                    || Math.Abs(a.G - b.G) > PerChannelTolerance
                    || Math.Abs(a.B - b.B) > PerChannelTolerance;

                if (isDifferent)
                {
                    differentPixels++;
                }

                diff.SetPixel(x, y, isDifferent ? Color.Red : Color.Black);
            }
        }

        var fraction = (double)differentPixels / (width * height);
        var matches = fraction <= toleranceFraction;
        if (matches)
        {
            diff.Dispose();
            return new ImageDiffResult(true, fraction, null);
        }

        return new ImageDiffResult(false, fraction, diff);
    }
}
