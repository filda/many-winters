using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ManyWinters.Tools.E2EHarness;

/// <summary>
/// Sends synthetic mouse/keyboard input to a specific window without requiring it to be
/// focused or in the foreground — PostMessage with WM_LBUTTONDOWN/UP and client-area
/// coordinates, confirmed to work against the standalone game window (see
/// project_beckett_lite_limits memory: Beckett Lite itself cannot inject input, but this
/// window-message approach, independent of Beckett, does).
/// TODO: confirm Godot's DisplayServer actually reacts to posted window messages the same
/// way it reacts to real hardware input; SendInput (which does move the real cursor) is the
/// fallback if PostMessage turns out not to reach Godot's input handling.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WindowInput
{
    private const uint WmMouseMove = 0x0200;
    private const uint WmLButtonDown = 0x0201;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmRButtonDown = 0x0204;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private static readonly IntPtr MkLButton = (IntPtr)0x0001;
    private static readonly IntPtr MkRButton = (IntPtr)0x0002;

    /// <summary>Sends a move, then a button-down/up pair, at client-area coordinates.</summary>
    public static void Click(IntPtr windowHandle, int clientX, int clientY, TimeSpan? pressDuration = null)
    {
        var lParam = MakeLParam(clientX, clientY);

        // A move before the down/up mirrors a real cursor: Godot's own picking (GroundPick,
        // HoverArbiter) resolves what's under the cursor from InputEventMouseMotion, so a click
        // with nothing establishing a cursor position first may hit whatever was last hovered
        // rather than (clientX, clientY). Unverified until this runs against the real game.
        Post(windowHandle, WmMouseMove, IntPtr.Zero, lParam);
        Post(windowHandle, WmLButtonDown, MkLButton, lParam);
        Thread.Sleep(pressDuration ?? TimeSpan.FromMilliseconds(50));
        Post(windowHandle, WmLButtonUp, IntPtr.Zero, lParam);
    }

    /// <summary>Sends a right-button press and release at one spot — the gesture the game reads
    /// as "what may be done here" (RightClickGesture: pressed and released in one spot, dragged
    /// it would turn the camera instead).</summary>
    public static void RightClick(IntPtr windowHandle, int clientX, int clientY, TimeSpan? pressDuration = null)
    {
        var lParam = MakeLParam(clientX, clientY);

        Post(windowHandle, WmMouseMove, IntPtr.Zero, lParam);
        Post(windowHandle, WmRButtonDown, MkRButton, lParam);
        Thread.Sleep(pressDuration ?? TimeSpan.FromMilliseconds(50));
        Post(windowHandle, WmRButtonUp, IntPtr.Zero, lParam);
    }

    /// <summary>Sends a key-down/up pair for a Win32 virtual-key code (see winuser.h VK_*).</summary>
    public static void KeyPress(IntPtr windowHandle, int virtualKeyCode, TimeSpan? pressDuration = null)
    {
        KeyDown(windowHandle, virtualKeyCode);
        Thread.Sleep(pressDuration ?? TimeSpan.FromMilliseconds(50));
        KeyUp(windowHandle, virtualKeyCode);
    }

    /// <summary>Presses a key down and leaves it down until KeyUp - for holds a test wants to
    /// end on a condition rather than on a timer (see GameFixture.HoldKeyUntilLog).</summary>
    public static void KeyDown(IntPtr windowHandle, int virtualKeyCode) =>
        Post(windowHandle, WmKeyDown, (IntPtr)virtualKeyCode, IntPtr.Zero);

    /// <summary>Releases a key pressed down by KeyDown.</summary>
    public static void KeyUp(IntPtr windowHandle, int virtualKeyCode) =>
        Post(windowHandle, WmKeyUp, (IntPtr)virtualKeyCode, IntPtr.Zero);

    // WM_MOUSEMOVE/WM_LBUTTON* lParam packs client coordinates as (y << 16) | x (MAKELPARAM).
    private static IntPtr MakeLParam(int x, int y) => (IntPtr)((y << 16) | (x & 0xFFFF));

    private static void Post(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (!NativeMethods.PostMessage(windowHandle, message, wParam, lParam))
        {
            throw new InvalidOperationException($"PostMessage(0x{message:X}) to window {windowHandle} failed.");
        }
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    }
}
