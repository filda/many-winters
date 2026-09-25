using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ManyWinters.Tools.E2EHarness;

/// <summary>
/// PrintWindow capture of a window's client area, same technique as build/ScreenshotTask.cs.
/// Deliberately not shared with it yet — a second use of the same P/Invoke is the copy-adapt
/// case, not the factor-it-out one; only worth merging if a third caller shows up.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WindowCapture
{
    private const uint PrintWindowClientOnly = 1;
    private const uint PrintWindowRenderFullContent = 2;

    public static Bitmap Capture(IntPtr windowHandle)
    {
        if (!NativeMethods.GetClientRect(windowHandle, out var rect))
        {
            throw new InvalidOperationException("GetClientRect failed.");
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("The window has an empty client area (minimised?).");
        }

        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        var hdc = graphics.GetHdc();
        try
        {
            if (!NativeMethods.PrintWindow(windowHandle, hdc, PrintWindowClientOnly | PrintWindowRenderFullContent))
            {
                throw new InvalidOperationException("PrintWindow failed.");
            }
        }
        finally
        {
            graphics.ReleaseHdc(hdc);
        }

        return bitmap;
    }

    /// <summary>The window's client-area size as the OS has it right now — the truth about what
    /// the captures and the click coordinates address, whatever size the game asked for at
    /// launch.</summary>
    internal static Size ClientSize(IntPtr windowHandle)
    {
        if (!NativeMethods.GetClientRect(windowHandle, out var rect))
        {
            throw new InvalidOperationException("GetClientRect failed.");
        }

        return new Size(rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetClientRect(IntPtr hWnd, out Rect rect);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    }
}
