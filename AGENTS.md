# Agent notes

Instructions for AI coding agents (Claude Code, OpenCode) working in this repository. `docs/development.md` (build, run, test, editor plugins) and `docs/conventions.md` (engineering rules) are the source of truth; read them first. This file does not repeat them, it collects what an agent tends to get wrong or cannot find in the repo.

## Working style

- **Do not commit.** The user reviews and commits each step. Leave the working tree with your changes and summarize them. Renames of tracked files go through `git mv`; say so, because it stages the rename.
- **Run the whole CI gate before reporting a change done**, not just the build: `dotnet format --verify-no-changes`, Release build, InspectCode, tests, exactly as `.github/workflows/ci.yml` runs them (see `docs/development.md`, "Inspections"). A green build misses unused `using` directives and dead members; only InspectCode reports those. On Windows InspectCode needs `--toolset-path`, documented in the same section.
- **No git hooks, ever.** Guards belong in the build (`Directory.Build.props`, `.editorconfig` severities) or in `ci.yml`.
- **LF line endings** in every file you create or edit, also on Windows. `.editorconfig` says so, but tools that default to CRLF have to be normalised afterwards.
- Dead code is deleted, never commented out, and new code ships with tests in the same change (`docs/conventions.md`).

## Godot editor and the Beckett MCP server

Beckett (`docs/development.md`, "Editor plugins") is an MCP server running inside the Godot editor. It exposes the scene tree, node inspection, running the game, screenshots and live runtime state as tools. Prefer it over scripting around the engine (parsing log files, launching the game from the CLI, Win32 screenshots) whenever it is available.

It is available only while the editor is open on `src/ManyWinters.Godot`. If the Beckett tools are missing or fail to connect, you may start the editor yourself, detached so it does not block your shell:

```powershell
Start-Process godot -ArgumentList '-e', '--path', 'src/ManyWinters.Godot'
```

Check first that it is not already running (a `Godot_v*` process, or a listener on port 8770): a second editor on the same project makes Beckett walk to the next free port and every client config goes stale. Startup takes a few seconds plus the C# build. The server is up once `Get-NetTCPConnection -LocalPort 8770` returns a listener. If the tools still do not show up, the MCP client connected before the server existed and needs a reconnect (Claude Code: `/mcp`).

The editor rewrites `project.godot` whenever it saves settings and drops every comment in the file. Rationale for a setting goes into `docs/development.md` ("Project settings"), never into `project.godot`. Editor addons are added through `plug.gd` and gd-plug, never by copying files into `addons/`.

## Fallbacks when the editor is closed

- **Game console output** (`GD.Print` from `ManyWinters.Godot` code) lands in `%APPDATA%\Godot\app_userdata\ManyWinters Godot\logs\godot.log`; previous runs sit alongside as `godot<timestamp>.log`. Read it instead of asking for pasted console output. `Console.WriteLine` from `ManyWinters.Core` does not reliably reach it; put temporary prints in the Godot-layer caller.
- **Stale textures after regenerating sprites.** Launching the game from the CLI (`godot --path src/ManyWinters.Godot`) reads textures from the import cache, not the PNGs. After `art/generate_sprites.py` or any generator overwrites a PNG, run `godot --path src/ManyWinters.Godot --headless --import` first, or you will be looking at the old sprites. It prints a harmless "Unable to start the timer" error.
- **Screenshot of the running game window** via Win32: find the window by PID (`EnumWindows`), take `GetClientRect`, then `PrintWindow(hwnd, hdc, 2)` (`PW_RENDERFULLCONTENT`). Do not use `SetForegroundWindow` plus `CopyFromScreen`: it reported success and captured whichever window was really on top.

## Project constraints not written in the code

- **Scattered decoration will become resources.** The trees, bushes, rocks, stumps, logs, grass, flowers and ferns placed by `TerrainRenderer.ScatterDecoration` are meant to turn into individually clickable `ResourceNode` entities. Never batch them into `MultiMeshInstance3D` or any instancing that erases per-node identity. Density problems are handled by lowering counts until spatial culling lands together with that conversion.
- **Sprite art follows the concept art's drawing, not just its motifs.** When asked to match `docs/ZemanConceptArt.png`, crop and enlarge the relevant region first and reproduce how it is drawn (silhouette construction, how hatching follows form), keeping the repository's palette and diagonal hatch fill underneath. Render a contact sheet at full and in-game scale (about 80 to 130 px), look at it, and iterate a few times before reporting.
