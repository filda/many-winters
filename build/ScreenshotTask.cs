using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace ManyWinters.Build;

/// <summary>
/// Saves a PNG of the running game's client area, whether or not the window is in front.
/// Arguments: <c>--out=path.png</c> (default: a timestamped file in the temp folder),
/// <c>--title=prefix</c> (default "ManyWinters Godot"; the editor, whose title ends in
/// "- Godot Engine", is skipped) or <c>--pid=n</c> to pick the window by process.
/// </summary>
[TaskName("Screenshot")]
public sealed class ScreenshotTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Screenshot uses Win32 PrintWindow and runs on Windows only.");
        }

        var outFile = context.Arguments.GetArgument("out")
            ?? Path.Combine(Path.GetTempPath(), "many-winters-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".png");
        outFile = Path.GetFullPath(outFile, context.RootDirectory);

        using var window = FindWindow(context.Arguments.GetArgument("pid"), context.Arguments.GetArgument("title") ?? "ManyWinters Godot");
        var (width, height) = WindowCapture.SavePng(window.MainWindowHandle, outFile);
        context.Log.Information(Verbosity.Normal, "{0} ({1}, {2}x{3}, pid {4})", outFile, window.MainWindowTitle, width, height, window.Id);
    }

    [SupportedOSPlatform("windows")]
    private static Process FindWindow(string? pid, string titlePrefix)
    {
        if (pid is not null)
        {
            var process = Process.GetProcessById(int.Parse(pid, CultureInfo.InvariantCulture));
            if (process.MainWindowHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Process {pid} has no main window.");
            }

            return process;
        }

        Process? found = null;
        foreach (var process in Process.GetProcesses())
        {
            if (found is null
                && process.MainWindowHandle != IntPtr.Zero
                && process.MainWindowTitle.StartsWith(titlePrefix, StringComparison.Ordinal)
                && !process.MainWindowTitle.Contains("- Godot Engine", StringComparison.Ordinal))
            {
                found = process;
            }
            else
            {
                process.Dispose();
            }
        }

        return found ?? throw new InvalidOperationException($"No window with a title starting '{titlePrefix}' found. Is the game running?");
    }
}

/// <summary>
/// PrintWindow with PW_CLIENTONLY | PW_RENDERFULLCONTENT reads the window's own composited
/// surface, so the capture is right even when the window is behind others. SetForegroundWindow
/// plus CopyFromScreen is not an alternative: it reports success and captures whichever window
/// is really on top.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WindowCapture
{
    private const uint PrintWindowClientOnly = 1;
    private const uint PrintWindowRenderFullContent = 2;

    public static (int Width, int Height) SavePng(IntPtr hwnd, string path)
    {
        if (!NativeMethods.GetClientRect(hwnd, out var rect))
        {
            throw new InvalidOperationException("GetClientRect failed.");
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("The window has an empty client area (minimised?).");
        }

        using var bitmap = new Bitmap(width, height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            var hdc = graphics.GetHdc();
            try
            {
                if (!NativeMethods.PrintWindow(hwnd, hdc, PrintWindowClientOnly | PrintWindowRenderFullContent))
                {
                    throw new InvalidOperationException("PrintWindow failed.");
                }
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
            }
        }

        bitmap.Save(path, ImageFormat.Png);
        return (width, height);
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
