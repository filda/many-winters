using System.Diagnostics;
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
        var process = StartBreakingAwayFromAnyRestrictiveJob(godotExe, "--path", godotProjectPath);

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
    /// the child from that job so it gets an ordinary, unrestricted top-level window; it only
    /// fails if the job explicitly disallows breakaway, which we'd rather surface than swallow.
    /// </summary>
    private static Process StartBreakingAwayFromAnyRestrictiveJob(string fileName, params string[] arguments)
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
            throw new InvalidOperationException($"CreateProcess('{fileName}', CREATE_BREAKAWAY_FROM_JOB) failed with Win32 error {error}.");
        }

        NativeMethods.CloseHandle(processInformation.HThread);
        NativeMethods.CloseHandle(processInformation.HProcess);
        return Process.GetProcessById(processInformation.DwProcessId);
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
