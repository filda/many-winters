using System.Drawing;
using System.Drawing.Imaging;
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

    public async Task InitializeAsync()
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

    public Task DisposeAsync()
    {
        _window?.Dispose();
        return Task.CompletedTask;
    }

    public void Click(int x, int y) => WindowInput.Click(Handle, x, y);

    public void RightClick(int x, int y) => WindowInput.RightClick(Handle, x, y);

    /// <summary>Holds a Win32 virtual-key code down for <paramref name="holdDuration"/> before releasing it — long enough for a held-key game action (e.g. camera tilt) to move, not just register.</summary>
    public void KeyPress(int virtualKeyCode, TimeSpan? holdDuration = null) => WindowInput.KeyPress(Handle, virtualKeyCode, holdDuration);

    // The Win32 virtual-key code for F12 - the game's "advance one tick" key (see Main._Input).
    private const int VkF12 = 0x7B;

    /// <summary>Steps the held clock forward exactly one tick (the game's "advance one tick"
    /// key), so an order placed while the clock stands - a craft, a building - is resolved once
    /// and the frame settles at the next fixed tick. The suite launches the game with the clock
    /// held (see GameWindow.LaunchAsync), so the world otherwise holds at the boot tick; in
    /// normal play the clock runs and the key is ignored.</summary>
    public void AdvanceOneTick() => KeyPress(VkF12);

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
    /// against the game's own log ("Inscription dismissed.", Ui/InscriptionOverlay.cs) and
    /// retried - still before anybody is selected, so a repeated click that missed the button
    /// could only land on the ground, which nobody is selected to walk.</summary>
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
