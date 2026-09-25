using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.Versioning;

namespace ManyWinters.Tools.E2EHarness;

/// <summary>
/// Launches the game windowed (<c>godot --path src/ManyWinters.Godot</c>) and gives a test
/// its window handle to drive and capture. One instance per test, not shared: each test starts
/// from the boot splash so state from a previous test can't leak into the next.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class GameWindow : IDisposable
{
    private readonly Process _process;
    private readonly string? _launcherScriptPath;

    private GameWindow(Process process, IntPtr handle, string? launcherScriptPath)
    {
        _process = process;
        Handle = handle;
        _launcherScriptPath = launcherScriptPath;
    }

    public IntPtr Handle { get; }

    /// <summary>The game process plus the Task Scheduler launcher script that started it, kept so
    /// cleanup can remove the script once the game (the script's last command) has exited.</summary>
    private readonly struct GameLaunch
    {
        public GameLaunch(Process process, string? launcherScriptPath)
        {
            Process = process;
            LauncherScriptPath = launcherScriptPath;
        }

        public Process Process { get; }

        public string? LauncherScriptPath { get; }
    }

    /// <summary>
    /// Starts the game and waits for it to report ready. Honors <c>MW_GODOT_EXE</c> (falling
    /// back to "godot" on PATH) — that's the env var the e2e-windows CI job sets to point at
    /// the downloaded editor binary; see .github/workflows/ci.yml.
    /// </summary>
    public static async Task<GameWindow> LaunchAsync(string godotProjectPath, TimeSpan timeout)
    {
        // The E2E always wants a reproducible frame, so the game runs with its real-time
        // presentation (person idle bob, live status-bar counters) frozen; see
        // DeterministicPresentation. Set on this process so a breakaway launch inherits it; the
        // Task Scheduler path bakes the same variable into its launcher script.
        Environment.SetEnvironmentVariable("MW_E2E_DETERMINISTIC", "1");

        var godotExe = Environment.GetEnvironmentVariable("MW_GODOT_EXE") ?? "godot";
        var launch = Start(godotExe, "--path", godotProjectPath);
        var process = launch.Process;

        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Godot", "app_userdata", "ManyWinters Godot", "logs", "godot.log");

        IntPtr lastHandle = IntPtr.Zero;
        var lastTitle = "";
        var lastLogReady = false;

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            // A fresh Process.GetProcessById each poll, not process.Refresh() on the object we
            // already hold: the game's window handle is not set until a frame or two after the
            // window exists, so re-probing each poll catches the moment it appears. The game is
            // launched via the Task Scheduler (see Start) - a fresh process tree with no calling
            // job - so its window is ordinary and visible to this host, not hidden by a job's
            // UI restriction.
            using var probe = SafeGetProcessById(process.Id);
            if (probe is null || probe.HasExited)
            {
                var exitCode = probe?.HasExited == true ? probe.ExitCode : (int?)null;
                process.Dispose();
                DeleteLauncherScript(launch.LauncherScriptPath);
                throw new InvalidOperationException($"Game process exited early{(exitCode is { } code ? $" with code {code}" : "")}. Check {logPath}.");
            }

            lastHandle = probe.MainWindowHandle;
            lastTitle = probe.MainWindowTitle;
            lastLogReady = LogShowsMainReady(logPath);
            if (lastHandle != IntPtr.Zero && lastLogReady)
            {
                // The loading page is freed with QueueFree (deferred to end-of-frame) and "Main
                // ready." prints in that same frame, so the first frames after the log can still
                // show it; let a few frames settle so the capture is the world, not the page.
                await Task.Delay(500);
                return new GameWindow(process, lastHandle, launch.LauncherScriptPath);
            }

            await Task.Delay(200);
        }

        var diagnostics = $"handle={lastHandle} title='{lastTitle}' logReady={lastLogReady} "
            + $"logExists={File.Exists(logPath)} logLength={(File.Exists(logPath) ? new FileInfo(logPath).Length : -1)}";
        KillProcessRobust(process);
        process.Dispose();
        DeleteLauncherScript(launch.LauncherScriptPath);
        throw new TimeoutException($"Game did not report ready within {timeout} ({diagnostics}). Check {logPath}.");
    }

    private static Process? SafeGetProcessById(int id)
    {
        try
        {
            return Process.GetProcessById(id);
        }
        catch (ArgumentException)
        {
            return null; // already gone
        }
    }

    /// <summary>
    /// "Main ready." is the last line <c>Main._Ready</c> prints (src/ManyWinters.Godot/Main.cs),
    /// once the world has finished building synchronously — the window itself can exist well
    /// before that, still showing the boot splash image. A fresh launch renames the previous
    /// "godot.log" away and starts a new one near-immediately at boot (see docs/development.md,
    /// "Reading the game's output"), well before the window even exists, so reading the whole
    /// current file rather than tracking a byte offset is safe in practice - an earlier version
    /// tried to detect the rotation by comparing lengths, but a new run's own log can grow past
    /// the previous run's final size before "Main ready." appears, which made that offset skip
    /// straight past the line it was looking for and never find it.
    /// </summary>
    private static bool LogShowsMainReady(string logPath)
    {
        if (!File.Exists(logPath))
        {
            return false;
        }

        using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Contains("Main ready.", StringComparison.Ordinal);
    }

    /// <summary>
    /// A plain <see cref="Process.Start(ProcessStartInfo)"/> puts the child in whatever job its
    /// caller belongs to; under a VSTest test host that job carries UI restrictions that make
    /// the child's own window invisible to window-handle lookups, from any process, once created
    /// that way (see the comment in <see cref="LaunchAsync"/>). CREATE_BREAKAWAY_FROM_JOB frees
    /// the child from that job so it gets an ordinary, unrestricted top-level window - and is all
    /// that's needed on a normal dev machine. On the GitHub-hosted Windows runner it instead
    /// fails with ERROR_ACCESS_DENIED (Win32 error 5): that job's policy disallows breakaway
    /// outright, confirmed 2026-09-22 on the e2e-windows CI job. The fallback there is Task
    /// Scheduler: a task it runs is a fresh process tree with no calling job at all, escaping
    /// the restriction by construction rather than needing permission to leave it - the price is
    /// that schtasks doesn't hand back the child's PID, so that path has to find the window by
    /// title instead (the same way ScreenshotTask.FindWindow does).
    /// </summary>
    private static GameLaunch Start(string fileName, params string[] arguments)
    {
        // Always launch via the Task Scheduler: a fresh process tree with no calling job gives the
        // game an ordinary, visible window the test host can find and drive. The
        // CREATE_BREAKAWAY_FROM_JOB alternative is denied by VSTest's job on the runner (Win32 5),
        // and on a dev machine where it is allowed the game's window comes up invisible to the test
        // host anyway (MainWindowHandle 0) - the scheduler path is the only one that reliably yields
        // a drivable window.
        var (process, launcherScriptPath) = StartViaScheduledTask(fileName, arguments);
        Console.Error.WriteLine($"[GameWindow] launched via Task Scheduler, pid {process.Id}, title '{process.MainWindowTitle}'.");
        return new GameLaunch(process, launcherScriptPath);
    }

    private static (Process Process, string LauncherScriptPath) StartViaScheduledTask(string fileName, string[] arguments)
    {
        var taskName = "ManyWintersE2E-" + Guid.NewGuid().ToString("N");

        // schtasks caps the task's command line (/TR) at 261 characters, which a fully-qualified
        // game binary plus project path can exceed (a WinGet-installed godot sits under a long
        // per-package directory). Hand the task a short-lived batch file instead: the task's
        // command is just the batch path, and the batch carries the real, possibly long, command.
        var launcherScriptPath = Path.Combine(Path.GetTempPath(), "ManyWintersE2E-" + Guid.NewGuid().ToString("N") + ".cmd");
        File.WriteAllText(
            launcherScriptPath,
            // The task scheduler runs in its own environment, not this process's, so the variable
            // LaunchAsync set here never reaches the game through this path - bake it in.
            "SET MW_E2E_DETERMINISTIC=1" + Environment.NewLine
            + Quote(fileName) + string.Concat(arguments.Select(argument => " " + Quote(argument))) + Environment.NewLine);

        RunSchtasks("/Create", "/TN", taskName, "/TR", Quote(launcherScriptPath), "/SC", "ONCE", "/ST", "00:00", "/F");
        try
        {
            RunSchtasks("/Run", "/TN", taskName);
            var process = FindWindowByTitle(TimeSpan.FromSeconds(15));
            return (process, launcherScriptPath);
        }
        finally
        {
            // Best-effort: a leaked one-off task next to hundreds of others is exactly the kind of
            // thing nobody notices until it's a mess, but a failure to delete it must not mask the
            // real result above.
            try
            {
                RunSchtasks("/Delete", "/TN", taskName, "/F");
            }
            catch (InvalidOperationException)
            {
            }

            // The batch is released once the game (its last command) exits; delete it here for the
            // no-game case. The success path deletes it again from cleanup after killing the game,
            // by which point the file is free.
            DeleteLauncherScript(launcherScriptPath);
        }
    }

    private static void RunSchtasks(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("schtasks.exe") { RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start schtasks.exe.");
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"schtasks.exe {string.Join(' ', arguments)} exited with {process.ExitCode}: {error}");
        }
    }

    /// <summary>
    /// Same filter as build/ScreenshotTask.cs.FindWindow: the game's window title starts with
    /// "ManyWinters Godot", the editor's ends in "- Godot Engine". Only needed by the Task
    /// Scheduler fallback, which has no PID to poll directly - the CI job it exists for never
    /// has an editor open on the project, so there's nothing else this could mistakenly match.
    /// </summary>
    private static Process FindWindowByTitle(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            foreach (var candidate in Process.GetProcesses())
            {
                if (candidate.MainWindowHandle != IntPtr.Zero
                    && candidate.MainWindowTitle.StartsWith("ManyWinters Godot", StringComparison.Ordinal)
                    && !candidate.MainWindowTitle.Contains("- Godot Engine", StringComparison.Ordinal))
                {
                    return candidate;
                }

                candidate.Dispose();
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException($"No 'ManyWinters Godot' window appeared within {timeout} after launching via Task Scheduler.");
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    /// <summary>Best-effort removal of a Task Scheduler launcher script. A failure must not mask
    /// the real test result; a leftover script in the temp dir is harmless and the OS reaps it.</summary>
    private static void DeleteLauncherScript(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    public void Dispose()
    {
        KillProcessRobust(_process);
        _process.Dispose();
        DeleteLauncherScript(_launcherScriptPath);
    }

    /// <summary>
    /// Terminates the game and its children. <c>Process.Kill(entireProcessTree: true)</c> cannot
    /// always kill a game the Task Scheduler fallback placed in the task's own job, and a leftover
    /// godot wedges the runner's end-of-job cleanup (the CI job hangs for minutes), so a forced
    /// <c>taskkill</c> by PID is the backstop. Best effort throughout: a failure here must not mask
    /// the real test result, so nothing in here throws.
    /// </summary>
    private static void KillProcessRobust(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // The game is in a job we can't re-parent for a tree kill; taskkill below still works.
        }

        try
        {
            if (!process.HasExited)
            {
                var startInfo = new ProcessStartInfo("taskkill.exe") { RedirectStandardError = true, UseShellExecute = false };
                startInfo.ArgumentList.Add("/F");
                startInfo.ArgumentList.Add("/T");
                startInfo.ArgumentList.Add("/PID");
                startInfo.ArgumentList.Add(process.Id.ToString(CultureInfo.InvariantCulture));
                using var taskkill = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start taskkill.exe.");
                taskkill.StandardError.ReadToEnd();
                taskkill.WaitForExit(5000);
            }
        }
        catch
        {
            // Best effort: never let cleanup throw and mask the real test result.
        }
    }
}
