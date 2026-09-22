using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

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

    private GameWindow(Process process, IntPtr handle)
    {
        _process = process;
        Handle = handle;
    }

    public IntPtr Handle { get; }

    /// <summary>
    /// Starts the game and waits for it to report ready. Honors <c>MW_GODOT_EXE</c> (falling
    /// back to "godot" on PATH) — that's the env var the e2e-windows CI job sets to point at
    /// the downloaded editor binary; see .github/workflows/ci.yml.
    /// </summary>
    public static async Task<GameWindow> LaunchAsync(string godotProjectPath, TimeSpan timeout)
    {
        var godotExe = Environment.GetEnvironmentVariable("MW_GODOT_EXE") ?? "godot";
        var process = Start(godotExe, "--path", godotProjectPath);

        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Godot", "app_userdata", "ManyWinters Godot", "logs", "godot.log");

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            // A fresh Process.GetProcessById each poll, not process.Refresh() on the object we
            // already hold: under `dotnet test`, testhost.exe runs inside a Windows Job Object
            // with UI restrictions (JOB_OBJECT_UILIMIT_HANDLES) that hides windows created by its
            // own children from itself specifically - confirmed by "Get-Process -Id <that pid>"
            // from an unrelated PowerShell session seeing the real MainWindowHandle immediately,
            // while this same check from inside testhost.exe never did, cached or not. Starting
            // the game with CREATE_BREAKAWAY_FROM_JOB (see StartBreakingAwayFromAnyRestrictiveJob)
            // is what actually fixes it; this fresh-probe habit is cheap insurance on top of that.
            using var probe = SafeGetProcessById(process.Id);
            if (probe is null || probe.HasExited)
            {
                var exitCode = probe?.HasExited == true ? probe.ExitCode : (int?)null;
                process.Dispose();
                throw new InvalidOperationException($"Game process exited early{(exitCode is { } code ? $" with code {code}" : "")}. Check {logPath}.");
            }

            if (probe.MainWindowHandle != IntPtr.Zero && LogShowsMainReady(logPath))
            {
                return new GameWindow(process, probe.MainWindowHandle);
            }

            await Task.Delay(200);
        }

        process.Kill(entireProcessTree: true);
        process.Dispose();
        throw new TimeoutException($"Game did not report ready within {timeout}. Check {logPath}.");
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
    private static Process Start(string fileName, params string[] arguments)
    {
        try
        {
            return StartBreakingAwayFromAnyRestrictiveJob(fileName, arguments);
        }
        catch (InvalidOperationException ex) when (ex.InnerException is Win32Exception { NativeErrorCode: 5 })
        {
            return StartViaScheduledTask(fileName, arguments);
        }
    }

    private static Process StartBreakingAwayFromAnyRestrictiveJob(string fileName, string[] arguments)
    {
        const uint createBreakawayFromJob = 0x0100_0000;

        var commandLineText = new StringBuilder(Quote(fileName));
        foreach (var argument in arguments)
        {
            commandLineText.Append(' ').Append(Quote(argument));
        }

        // CreateProcess can write back into this buffer, so it has to be a real mutable array,
        // not a string - CA1838 flags StringBuilder here for the same reason.
        var commandLineBuffer = new char[commandLineText.Length + 1];
        commandLineText.CopyTo(0, commandLineBuffer, 0, commandLineText.Length);

        var startupInfo = new NativeMethods.StartupInfo { Cb = Marshal.SizeOf<NativeMethods.StartupInfo>() };
        if (!NativeMethods.CreateProcess(
                null, commandLineBuffer, IntPtr.Zero, IntPtr.Zero, false, createBreakawayFromJob,
                IntPtr.Zero, null, ref startupInfo, out var processInformation))
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"CreateProcess('{fileName}', CREATE_BREAKAWAY_FROM_JOB) failed with Win32 error {error}.",
                new Win32Exception(error));
        }

        NativeMethods.CloseHandle(processInformation.HThread);
        NativeMethods.CloseHandle(processInformation.HProcess);
        return Process.GetProcessById(processInformation.DwProcessId);
    }

    private static Process StartViaScheduledTask(string fileName, string[] arguments)
    {
        var taskName = "ManyWintersE2E-" + Guid.NewGuid().ToString("N");
        var action = Quote(fileName) + string.Concat(arguments.Select(argument => " " + Quote(argument)));

        RunSchtasks("/Create", "/TN", taskName, "/TR", action, "/SC", "ONCE", "/ST", "00:00", "/F");
        try
        {
            RunSchtasks("/Run", "/TN", taskName);
            return FindWindowByTitle(TimeSpan.FromSeconds(15));
        }
        finally
        {
            // Best-effort: the task's job is done once it's launched the process, and a leaked
            // one-off task next to hundreds of others is exactly the kind of thing nobody notices
            // until it's a mess, but a failure to delete it must not mask the real result above.
            try
            {
                RunSchtasks("/Delete", "/TN", taskName, "/F");
            }
            catch (InvalidOperationException)
            {
            }
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

    public void Dispose()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
        }

        _process.Dispose();
    }

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct StartupInfo
        {
            public int Cb;
            public IntPtr LpReserved;
            public IntPtr LpDesktop;
            public IntPtr LpTitle;
            public int DwX;
            public int DwY;
            public int DwXSize;
            public int DwYSize;
            public int DwXCountChars;
            public int DwYCountChars;
            public int DwFillAttribute;
            public int DwFlags;
            public short WShowWindow;
            public short CbReserved2;
            public IntPtr LpReserved2;
            public IntPtr HStdInput;
            public IntPtr HStdOutput;
            public IntPtr HStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ProcessInformation
        {
            public IntPtr HProcess;
            public IntPtr HThread;
            public int DwProcessId;
            public int DwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CreateProcess(
            string? lpApplicationName,
            [In, Out] char[] lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string? lpCurrentDirectory,
            ref StartupInfo lpStartupInfo,
            out ProcessInformation lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);
    }
}
