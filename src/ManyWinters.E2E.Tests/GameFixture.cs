using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using ManyWinters.Tools.E2EHarness;

namespace ManyWinters.E2E.Tests;

/// <summary>
/// Per-test game instance: launches the windowed game fresh, exposes input/capture, and kills
/// the process afterwards. xunit constructs and disposes one of these per test that uses it, so
/// tests don't see each other's state — the price is a slow launch per test, acceptable while
/// the suite is a handful of golden-path scenarios rather than many small tests.
/// </summary>
public sealed class GameFixture : IAsyncLifetime
{
    private GameWindow? _window;
    private long _gameLogOffset;

    public async ValueTask InitializeAsync()
    {
        // 60 s, not 30: the presenter builds a view for every resource node (thousands), so a
        // cold boot - first run after a build, or a loaded machine - can exceed 30 s before
        // "Main ready." lands. A timeout here also kills the game, so a too-tight value wastes a
        // whole slow boot.
        _window = await GameWindow.LaunchAsync(GodotProjectPath(), TimeSpan.FromSeconds(60));

        // Game-log lines from here on are this test's own doing: the boot's lines (the prologue
        // inscription, "Main ready.") all precede this point, so a later WaitForGameLog cannot
        // match one of them.
        _gameLogOffset = File.Exists(GameLogPath()) ? new FileInfo(GameLogPath()).Length : 0;
    }

    public ValueTask DisposeAsync()
    {
        _window?.Dispose();
        AssertBootLogHasNoScriptError();
        return ValueTask.CompletedTask;
    }

    // The whole run's log, not the per-test offset: a script error at boot (building a view for
    // every entity) precedes every test's own offset, and the tests above it can still pass while
    // it sits there unread - this is the one place that reads the file from its start. Thrown
    // rather than asserted with xunit's Assert: this runs from IAsyncLifetime.DisposeAsync, not a
    // [Fact], and xunit surfaces an exception from here as this fixture's own failure regardless.
    private static void AssertBootLogHasNoScriptError()
    {
        var path = GameLogPath();
        if (!File.Exists(path))
        {
            return;
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var log = reader.ReadToEnd();

        var match = Regex.Match(log, @"NullReferenceException|at ManyWinters\.");
        if (match.Success)
        {
            var context = log[Math.Max(0, match.Index - 200)..Math.Min(log.Length, match.Index + 400)];
            throw new InvalidOperationException($"The game's log contains a script error:\n...{context}...");
        }
    }

    public void Click(int x, int y) => WindowInput.Click(Handle, x, y);

    public void RightClick(int x, int y) => WindowInput.RightClick(Handle, x, y);

    /// <summary>Holds a Win32 virtual-key code down for <paramref name="holdDuration"/> before releasing it — long enough for a held-key game action (e.g. camera tilt) to move, not just register.</summary>
    public void KeyPress(int virtualKeyCode, TimeSpan? holdDuration = null) => WindowInput.KeyPress(Handle, virtualKeyCode, holdDuration);

    // The Win32 virtual-key code for F12, the game's "advance one tick" key.
    private const int VkF12 = 0x7B;

    /// <summary>Steps the held clock forward exactly one tick (the game's "advance one tick"
    /// key), so an order placed while the clock stands - a craft, a building - is resolved once
    /// and the frame settles at the next fixed tick. The suite launches the game with the clock
    /// held, so the world otherwise holds at the boot tick; in normal play the clock runs and the
    /// key is ignored.</summary>
    public void AdvanceOneTick() => KeyPress(VkF12);

    /// <summary>Presses a key down and holds it until the game's log gains a line containing
    /// <paramref name="text"/>, then releases it - returning that line, or null once
    /// <paramref name="timeout"/> has passed. The release always happens, timeout or not. A
    /// fixed-length hold cannot be made reliable on a machine whose frames are slower than the
    /// hold: a key-down and its key-up can then be pumped in the same frame, and the game never
    /// sees the key held at all. Waiting for the effect ends the hold by construction only once
    /// the effect exists.</summary>
    public string? HoldKeyUntilLog(int virtualKeyCode, string text, TimeSpan timeout)
    {
        WindowInput.KeyDown(Handle, virtualKeyCode);
        try
        {
            return WaitForGameLog(text, timeout);
        }
        finally
        {
            WindowInput.KeyUp(Handle, virtualKeyCode);
        }
    }

    public Bitmap Screenshot() => WindowCapture.Capture(Handle);

    // The prologue inscription's "closing words" button, which dismisses it. Every test boots into
    // the same deterministic world (the band name is seed-derived and fixed), so the button always
    // renders at the same place and this is a calibration, not a guess. Measured off a recorded
    // boot frame (the parchment slip under the title), not derived by hand.
    private const int PrologueClosingX = 568;
    private const int PrologueClosingY = 362;

    /// <summary>Dismisses the prologue inscription (the "closing words" button) so clicks reach the
    /// world. The dismissal primes the tick accumulator, so the world resumes on the next frame and
    /// is settled at its first live tick; the rest of the tick interval (one second) is well clear
    /// of the follow-on input, so the frame the test captures is a fixed tick, not a moving one.
    /// A posted click can be swallowed on a stuttering machine, so the dismissal is verified
    /// against the game's own log ("Inscription dismissed.") and retried - still before anybody
    /// is selected, so a repeated click that missed the button could only land on the ground,
    /// which nobody is selected to walk.</summary>
    public void DismissPrologue()
    {
        Click(PrologueClosingX, PrologueClosingY);
        if (WaitForGameLog("Inscription dismissed", TimeSpan.FromSeconds(2)) is null)
        {
            Click(PrologueClosingX, PrologueClosingY);
            WaitForGameLog("Inscription dismissed", TimeSpan.FromSeconds(2));
        }

        Thread.Sleep(600);
    }

    /// <summary>Waits until the game's log gains a line containing <paramref name="text"/> and
    /// returns that line, or null once <paramref name="timeout"/> has passed. The game's own log
    /// is the one honest witness of what a posted click did - nothing outside the process can
    /// read its state - so what the suite asserts, it asserts on these lines. Scans only lines
    /// written since the last match, so two waits for the same kind of line cannot both match
    /// the first one.</summary>
    public string? WaitForGameLog(string text, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var path = GameLogPath();
            if (File.Exists(path))
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                stream.Seek(_gameLogOffset, SeekOrigin.Begin);
                using var reader = new StreamReader(stream);
                var rest = reader.ReadToEnd();
                var match = rest.IndexOf(text, StringComparison.Ordinal);
                if (match >= 0)
                {
                    var endOfLine = rest.IndexOf('\n', match);
                    _gameLogOffset += endOfLine >= 0 ? endOfLine + 1 : rest.Length;
                    return rest[match..(endOfLine >= 0 ? endOfLine : rest.Length)].TrimEnd('\r');
                }
            }

            Thread.Sleep(100);
        }

        return null;
    }

    /// <summary>Reads the last "E2E anchor <paramref name="kind"/> x y" line written since the
    /// current log offset - printed once right after "Inscription dismissed." on every prologue -
    /// null if there is no such line yet, or "... none" was printed for it (nothing of that kind
    /// was on screen to click). A test calls this right after DismissPrologue, before anything
    /// else is waited for: unlike WaitForGameLog this peeks rather than advancing the offset,
    /// because the anchor lines are never themselves the text a later wait looks for, so leaving
    /// them in the unread tail is harmless and more than one anchor can each be read once.</summary>
    public (int X, int Y)? ReadAnchor(string kind)
    {
        var path = GameLogPath();
        if (!File.Exists(path))
        {
            return null;
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Seek(_gameLogOffset, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);
        var rest = reader.ReadToEnd();

        var match = Regex.Match(rest, $@"E2E anchor {Regex.Escape(kind)} (\d+) (\d+)");
        return match.Success
            ? (int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture))
            : null;
    }

    private static string GameLogPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Godot", "app_userdata", "ManyWinters Godot", "logs", "godot.log");

    /// <summary>Waits for the game to present the frame after the last posted input. A capture
    /// taken sooner races the render: the world state is already there (the tick log proves it)
    /// but the frame still shows the previous one, so a status bar or a freshly opened panel
    /// lags a click by one to three frames.</summary>
    public static void Settle(int milliseconds) => Thread.Sleep(milliseconds);

    /// <summary>Writes the current frame to artifacts/e2e-debug/{name}.png for inspection - never
    /// asserted. What the suite claims, it claims through the game's own log; the frames are for
    /// the human reviewing a failure or a change.</summary>
    public void SaveDebugShot(string name)
    {
        using var shot = Screenshot();
        var directory = Path.Combine(FindRepoRoot(), "artifacts", "e2e-debug");
        Directory.CreateDirectory(directory);
        shot.Save(Path.Combine(directory, name + ".png"), ImageFormat.Png);
    }

    private IntPtr Handle => _window?.Handle ?? throw new InvalidOperationException("GameFixture not initialized.");

    private static string GodotProjectPath() =>
        Path.Combine(FindRepoRoot(), "src", "ManyWinters.Godot");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ManyWinters.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find repository root above " + AppContext.BaseDirectory);
    }
}
