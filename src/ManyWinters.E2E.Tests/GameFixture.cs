using System.Drawing;
using System.Drawing.Imaging;
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

    public async Task InitializeAsync()
    {
        _window = await GameWindow.LaunchAsync(GodotProjectPath(), TimeSpan.FromSeconds(30));
    }

    public Task DisposeAsync()
    {
        _window?.Dispose();
        return Task.CompletedTask;
    }

    public void Click(int x, int y) => WindowInput.Click(Handle, x, y);

    /// <summary>Holds a Win32 virtual-key code down for <paramref name="holdDuration"/> before releasing it — long enough for a held-key game action (e.g. camera tilt) to move, not just register.</summary>
    public void KeyPress(int virtualKeyCode, TimeSpan? holdDuration = null) => WindowInput.KeyPress(Handle, virtualKeyCode, holdDuration);

    public Bitmap Screenshot() => WindowCapture.Capture(Handle);

    /// <summary>The window's current client-area size, for computing click targets that scale with the window instead of hardcoding pixels.</summary>
    public Size WindowSize()
    {
        using var screenshot = Screenshot();
        return screenshot.Size;
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
