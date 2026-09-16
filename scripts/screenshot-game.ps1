<#
.SYNOPSIS
Saves a PNG of the running game's client area, whether or not the window is in front.

Uses PrintWindow with PW_CLIENTONLY | PW_RENDERFULLCONTENT, which reads the window's own
composited surface. SetForegroundWindow plus CopyFromScreen is not an alternative: it reports
success and captures whichever window is really on top.

.PARAMETER Title
Wildcard for the main window title. The game runs as "ManyWinters Godot" (with a "(DEBUG)"
suffix when started from the editor); the editor itself ends in "- Godot Engine" and is skipped.
#>
param(
    [string]$OutFile = (Join-Path ([IO.Path]::GetTempPath()) ("many-winters-{0:yyyyMMdd-HHmmss}.png" -f (Get-Date))),
    [string]$Title = "ManyWinters Godot*",
    [int]$ProcessId
)
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing
Add-Type -Name Win32 -Namespace Screenshot -MemberDefinition @"
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct RECT { public int Left, Top, Right, Bottom; }
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool GetClientRect(System.IntPtr hWnd, out RECT rect);
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool PrintWindow(System.IntPtr hWnd, System.IntPtr hdc, uint flags);
"@

$candidates = Get-Process | Where-Object { $_.MainWindowHandle -ne 0 }
if ($ProcessId) {
    $candidates = $candidates | Where-Object Id -eq $ProcessId
}
else {
    $candidates = $candidates | Where-Object {
        $_.MainWindowTitle -like $Title -and $_.MainWindowTitle -notlike "*- Godot Engine*"
    }
}
$game = $candidates | Select-Object -First 1
if (-not $game) { throw "No window matching '$Title' found. Is the game running?" }

$rect = New-Object Screenshot.Win32+RECT
[void][Screenshot.Win32]::GetClientRect($game.MainWindowHandle, [ref]$rect)
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
if ($width -le 0 -or $height -le 0) { throw "Window of pid $($game.Id) has an empty client area (minimised?)." }

$bitmap = New-Object Drawing.Bitmap $width, $height
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$hdc = $graphics.GetHdc()
try {
    $PW_CLIENTONLY = 1
    $PW_RENDERFULLCONTENT = 2
    if (-not [Screenshot.Win32]::PrintWindow($game.MainWindowHandle, $hdc, $PW_CLIENTONLY -bor $PW_RENDERFULLCONTENT)) {
        throw "PrintWindow failed for window $($game.MainWindowHandle)."
    }
}
finally {
    $graphics.ReleaseHdc($hdc)
    $graphics.Dispose()
}

if (-not [IO.Path]::IsPathRooted($OutFile)) { $OutFile = Join-Path $PWD $OutFile }
$bitmap.Save($OutFile, [Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()
Write-Host "$OutFile ($($game.MainWindowTitle), ${width}x${height}, pid $($game.Id))"
