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

    /// <summary>The window's current client-area size, for computing click targets that scale with the window instead of hardcoding pixels.</summary>
    public Size WindowSize()
    {
        using var screenshot = Screenshot();
        return screenshot.Size;
    }

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
        if (!WaitForGameLog("Inscription dismissed", TimeSpan.FromSeconds(2)))
        {
            Click(PrologueClosingX, PrologueClosingY);
            WaitForGameLog("Inscription dismissed", TimeSpan.FromSeconds(2));
        }

        Thread.Sleep(600);
    }

    /// <summary>Waits until the game's log gains a line containing <paramref name="text"/> — the
    /// game's own witness that a posted click did what it meant, since nothing else can read the
    /// game's state from outside. Scans only lines written since the last match, so two waits for
    /// the same kind of line cannot both match the first one.</summary>
    public bool WaitForGameLog(string text, TimeSpan timeout)
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
                    return true;
                }
            }

            Thread.Sleep(100);
        }

        return false;
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
    /// asserted. The calibration aid: click targets are read off a real run's frame, not derived.
    /// Mirrors the on-failure dumps AssertMatchesBaseline already writes to artifacts/e2e-diffs.</summary>
    public void SaveDebugShot(string name)
    {
        using var shot = Screenshot();
        var directory = Path.Combine(FindRepoRoot(), "artifacts", "e2e-debug");
        Directory.CreateDirectory(directory);
        shot.Save(Path.Combine(directory, name + ".png"), ImageFormat.Png);
    }

    /// <summary>
    /// Compares against src/ManyWinters.E2E.Tests/Baselines/{name}.png. Set
    /// <c>MW_E2E_UPDATE_BASELINES=1</c> to (re-)record it instead of asserting, for a first run
    /// or a deliberate visual change — review the written PNG before committing it, the same way
    /// a code change gets reviewed rather than just accepted because the tool produced it.
    /// </summary>
    public void AssertMatchesBaseline(string name, double toleranceFraction = 0.01)
    {
        var baselinePath = Path.Combine(FindRepoRoot(), "src", "ManyWinters.E2E.Tests", "Baselines", name + ".png");
        using var actual = Screenshot();

        if (Environment.GetEnvironmentVariable("MW_E2E_UPDATE_BASELINES") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
            actual.Save(baselinePath, ImageFormat.Png);
            return;
        }

        if (!File.Exists(baselinePath))
        {
            throw new InvalidOperationException(
                $"No baseline at {baselinePath}. Run once with MW_E2E_UPDATE_BASELINES=1, review the PNG, then commit it.");
        }

        using var baseline = new Bitmap(baselinePath);
        var result = ImageDiff.Compare(actual, baseline, toleranceFraction);
        if (result.Matches)
        {
            return;
        }

        using (result.DiffImage)
        {
            var diffDirectory = Path.Combine(FindRepoRoot(), "artifacts", "e2e-diffs");
            Directory.CreateDirectory(diffDirectory);
            actual.Save(Path.Combine(diffDirectory, name + ".actual.png"), ImageFormat.Png);
            result.DiffImage?.Save(Path.Combine(diffDirectory, name + ".diff.png"), ImageFormat.Png);

            Assert.Fail(
                $"{name}: {result.DifferentPixelFraction:P2} of pixels differ from the baseline (tolerance {toleranceFraction:P2}). "
                + $"See {diffDirectory}.");
        }
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
