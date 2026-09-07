# Development Guide

How to set up a machine, build, run, and check Of Folk and Many Winters. For what the game *is*, start with the repository [`README.md`](../README.md); for what is being built next, see [`roadmap.md`](roadmap.md) and [`status.md`](status.md).

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0 (LTS) | Required to build the C# simulation core and tools. |
| [Godot Engine — .NET/Mono build](https://godotengine.org/download) | 4.7.x | Must be the **.NET** build specifically — the standard Godot build does not support C#. |
| IDE with C# support | — | [JetBrains Rider](https://www.jetbrains.com/rider/) is recommended (best Godot debugger integration). VS Code with the C# Dev Kit and Godot extensions also works. |
| Git | any recent | |

### Installing on Windows via winget

```powershell
winget install --id Microsoft.DotNet.SDK.8 -e
winget install --id GodotEngine.GodotEngine.Mono -e
```

After installation, restart your shell so the updated `PATH` takes effect, then verify:

```powershell
dotnet --version
godot --version
```

## Getting the code

```powershell
git clone <repository-url>
cd many-winters
```

## Project structure

Per the [technical implementation plan](<Of Folk and Many Winters — Technical Implementation Plan.md>):

```text
src/
├── ManyWinters.Core/          # Pure C# simulation — no Godot dependency, ever
├── ManyWinters.Godot/         # Godot project: presentation, rendering, input, UI, audio
│   ├── Logic/                 #   engine-free, unit-tested logic — the only mutated folder
│   ├── Views/                 #   one Node3D per simulation entity, plus WorldPresenter
│   ├── Sprites/               #   billboards, hit testing, extents, tint, texture cache
│   ├── Terrain/               #   heightmap mesh and waterways
│   ├── Fog/                   #   fog of war and the cloud banks over it
│   ├── Interaction/           #   camera rig, ground picking, hover fallback
│   ├── Ui/                    #   status bar and floating panels
│   └── Prototypes/            #   experiment scenes, held to a lower bar (see conventions)
├── ManyWinters.Tools/
│   └── SimulationRunner/      # Headless console runner (no Godot required)
├── ManyWinters.Tests/         # Tests for ManyWinters.Core and the SimulationRunner
└── ManyWinters.Godot.Tests/   # Tests for the presentation layer's own calculations
```

`ManyWinters.Core` must never reference `ManyWinters.Godot`. The simulation must be runnable and testable headlessly, without the engine.

Inside the Godot project, namespaces follow folders — ReSharper's `CheckNamespace` inspection enforces it, so a move means a namespace change and a `using` at each consumer. Two things stay at the project root because Godot pins them by path, and getting either wrong fails *silently* rather than at build time: `Main.cs` (named in `Main.tscn`) and the two `*VisualDefinition.cs` files (named in 17 `.tres` files under `Content/`, whose loader falls back to a default colour rather than complaining). **A folder must not be named after a Godot type** — an `Input/` folder shadows the `Input` singleton and every `Input.IsKeyPressed` call in it stops compiling.

## Building and running

```powershell
# Build everything
dotnet build

# Run the headless simulation runner (no Godot required)
dotnet run --project src/ManyWinters.Tools/SimulationRunner

# Run tests
dotnet test
```

Every run of the tool starts a fresh, empty world — nothing persists between separate invocations unless you explicitly `save`/`load` it. The world runs on the same content the game ships with, loaded from `src/ManyWinters.Godot/Content` relative to the working directory (so run it from the repository root, or point it elsewhere with `--content <dir>` as the first argument). Chain as many commands as you want into a single invocation, unquoted:

```powershell
dotnet run --project src/ManyWinters.Tools/SimulationRunner -- generate create 100 simulate 1 print population
```

To carry state across multiple invocations (e.g. separate sessions), bridge it through a save file:

```powershell
dotnet run --project src/ManyWinters.Tools/SimulationRunner -- generate create 100 simulate 1 save world.json
# ...later...
dotnet run --project src/ManyWinters.Tools/SimulationRunner -- load world.json print population
```

`dotnet run` re-checks whether a rebuild is needed on every invocation, which costs about a second even when nothing changed. For faster manual iteration on the runner, skip that check:

```powershell
# Option 1: skip the up-to-date check (still runs the build system)
dotnet run --project src/ManyWinters.Tools/SimulationRunner --no-build -- <command> [<command> ...]

# Option 2: build once, then invoke the compiled executable directly
dotnet build src/ManyWinters.Tools/SimulationRunner
./src/ManyWinters.Tools/SimulationRunner/bin/Debug/net8.0/ManyWinters.Tools.SimulationRunner.exe <command> [<command> ...]
```

Both require a build to already exist (`--no-build` fails otherwise) and skip re-checking that it's current — rebuild manually after changing code.

To run the game with rendering, open `src/ManyWinters.Godot` in the Godot editor, or launch it directly:

```powershell
godot --path src/ManyWinters.Godot
```

If Godot reports that the C# project needs to be built (e.g. right after cloning, or after pulling changes), build it and try again:

```powershell
dotnet build src/ManyWinters.Godot
```

`dotnet build` writes straight into the assembly Godot loads (`src/ManyWinters.Godot/.godot/mono/temp/bin/`), so no separate editor-side build step is needed.

### Project settings

The editor rewrites `project.godot` whenever it saves a setting (opening the project is enough) and drops every comment in the file, so the reasons behind non-default values live here instead.

- **`application/boot_splash/image`** is `res://Content/splash/title_page.png`, generated by `art/generate_splash.py`: an engraved title page shown instead of the Godot logo for the whole synchronous world build in `Main._Ready`. `boot_splash/bg_color` matches `default_clear_color` below so the letterboxed margins and the first rendered frame share one colour.
- **`rendering/environment/defaults/default_clear_color`** is `Color(0.32, 0.29, 0.26)`, a warm grey-brown instead of Godot's neutral grey: the empty sky is the table the chronicle's map lies on. It frames the parchment of the unknown fog tier (which fades into this exact colour; `FogOfWarRenderer` passes it to the shader) and lets the cool grey clouds read against it. A neutral grey void made the whole scene read as an unlit 3D scene, and a near-black sepia (0.17) was tried and was too heavy.

### Editor plugins

Editor-only addons live in `src/ManyWinters.Godot/addons/`. Like NuGet packages they are **not committed**: the one exception is [gd-plug](https://github.com/imjp94/gd-plug), a single-file plugin manager (`addons/gd-plug/plug.gd`) that restores the rest. `src/ManyWinters.Godot/plug.gd` is the manifest; each line pins a GitHub repository to a tag:

```gdscript
extends "res://addons/gd-plug/plug.gd"

func _plugging():
	plug("beckettlab/beckett-godot-mcp", {"tag": "v1.15.0"})
```

**Restore — after a fresh clone, and again after every edit to `plug.gd` — from the repo root:**

```powershell
godot --headless --path src/ManyWinters.Godot -s plug.gd install
```

It needs `git` on `PATH`. Each repository is cloned into `src/ManyWinters.Godot/.plugged/` (gitignored) and its `addons/` folder is copied into ours; Beckett takes about two seconds. To update a plugin, bump its tag and run the command again (a changed pin re-clones); to remove one, delete its line and run it again (unlisted plugins are uninstalled). `status` instead of `install` lists what is installed. Two things to ignore: gd-plug exits non-zero (-1, which Git Bash reports as 127) even when it succeeded, so read its log rather than the exit code, and a fresh-clone run opens with three `ERROR` lines about the `BeckettRuntime` autoload, because `project.godot` already references the addon that this very run is about to install.

A clone that skips this step is not merely noisy: the editor drops the missing plugin from `project.godot`, and Godot logs `ERROR` lines for the dangling autoload on every import, export and game start, which is what the CI release job fails on. That job therefore runs the same command before importing.

`addons/gd-plug/plug.gd` is upstream `master` at commit `209276d1f00d14b49b74403d9839f29598e9a8eb` (2026-05-23); the last tagged release (0.2.6, 2024) predates fixes needed on Godot 4.4+. To update it, replace the file from upstream.

**Beckett.** [Beckett](https://github.com/beckettlab/beckett-godot-mcp) is a free, MIT-licensed MCP server that runs *inside* the Godot editor, so an AI assistant (Claude Code, OpenCode and others) can inspect scenes, write scripts, run the game, and read the live tree. Nothing in the build or the exported game needs it: its export filter strips it from the pack, leaving only an inert autoload stub. Open `src/ManyWinters.Godot` in the Godot editor after restoring it. `project.godot` already enables the plugin, and it starts on boot — you'll see `[beckett] server listening on http://127.0.0.1:8770/...` in the output. The server lives inside the editor process: close the editor and every MCP call fails until it is open again.

**Connect an agent — run from the repo root, after the first editor start:**

Beckett writes its client config next to `project.godot` (`src/ManyWinters.Godot/.mcp.json`, plus a `.vscode/mcp.json` we don't use), but the agents run from the repo root and look there: Claude Code reads `.mcp.json`, OpenCode reads `opencode.json` and does not read `.mcp.json` at all. The URL also carries a per-machine auth token from `src/ManyWinters.Godot/.beckett/token`, so all three files are gitignored and each machine generates its own root copies:

```powershell
$token = (Get-Content src/ManyWinters.Godot/.beckett/token -Raw).Trim()
$port = (Get-Content src/ManyWinters.Godot/.beckett/port -Raw).Trim()
$url = "http://127.0.0.1:$port/mcp/$token"
$lf = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText("$PWD/.mcp.json", "{`n  `"mcpServers`": {`n    `"beckett`": {`n      `"type`": `"http`",`n      `"url`": `"$url`"`n    }`n  }`n}`n", $lf)
[IO.File]::WriteAllText("$PWD/opencode.json", "{`n  `"`$schema`": `"https://opencode.ai/config.json`",`n  `"mcp`": {`n    `"beckett`": {`n      `"type`": `"remote`",`n      `"url`": `"$url`",`n      `"enabled`": true`n    }`n  }`n}`n", $lf)
```

Re-run it if the token is regenerated or the server reports a different port (it walks past a busy 8770).

## Testing the presentation layer

Most of `ManyWinters.Godot` is engine wiring, but the calculations mixed into it are ordinary functions worth pinning. `docs/conventions.md` asks for them to be written apart from the code the framework calls; this is what that means in practice here, and what the engine allows.

**What runs outside the engine, verified rather than assumed:**

- **Godot's math value types are plain managed structs** — `Vector2`, `Vector3`, `Basis`, `Transform3D`, `Color`, `Mathf`, including `Color.FromHsv`. A normal xunit project can reference `ManyWinters.Godot` and call any static method that only uses those.
- **Anything `Node`- or `Resource`-derived aborts the whole test host.** `new Node3D()`, `Image`, `Texture2D`, `Camera3D`, `Sprite3D`, `SurfaceTool`, `ResourceLoader`, `Godot.FileAccess` — these enter a native runtime that is not initialised outside the editor. It is not a catchable exception: the process dies, so one such test takes every other test in that project down with it.
- **Mocking cannot cross that line.** `new Mock<Node3D>()` fails with `AccessViolationException`, because a mock of a *class* is a generated subclass whose constructor still calls the real one. Godot node types are classes, not interfaces, so no mocking library helps. A self-defined interface seam works in principle, but for pure wiring it only ever asserts the mock's own script.
- **Watch for *indirect* engine access.** A method can look perfectly pure and still reach the runtime underneath — `ResourceNodeView.BaseTexturePathFor` calls `HasTreeSprite`, which calls `ResourceLoader.Exists`; `BuildingView.ColorFor` loads a `.tres`. Check what a candidate actually calls before assuming it is extractable.

**Where an extracted calculation goes.** The signature decides: anything mentioning `Color`, `Vector3` or a sprite extent stays in `ManyWinters.Godot` and is tested from `ManyWinters.Godot.Tests`, because Core must never reference Godot. Only genuinely engine-free logic moves to Core (`BoxBlur`, `CloudSpotScatter`, `GroundCloudCoverage`, `ExplorationState` and `GridDistanceField` all arrived that way). Either way it becomes an `internal` type reached through `InternalsVisibleTo`, so the public API does not widen, and it gets its own small purpose-named file rather than staying where it sat. On the Godot side that file goes in `src/ManyWinters.Godot/Logic/` — engine-free, unit-tested logic only, which is exactly what the mutation config globs, so nothing has to be remembered when something new lands there. Anything in that folder that reaches an engine type has been put in the wrong place.

Testing the *wiring* itself — does a view add the right children, does a signal connect — would need a Godot-hosted runner such as gdUnit4 or GoDotTest, with a Godot binary and a headless display in CI. Not set up, and not worth it while the wiring is not producing bugs.

**Wiring that is deliberately left untested.** `TerrainSetup`, `CloudScatter.Scatter`, `BillboardSprite.Create`, `GroundShadow.Create`, `TextureCache`, `ContentFiles`, `CloudFogMask`, every `_Ready`/`_Process`/`_ExitTree`/`OnInputEvent`, the views' `IsStillUnderCursor` (which only asks the viewport where the cursor is and hands the answer to an already-tested pixel test), and the `On*ButtonPressed` handlers in `Main` carry no decision of their own — a test would assert that the implementation is the implementation. Every calculation worth splitting out of the presentation layer has been split out; the next one arrives with the code that needs it rather than off a backlog.

## Mutation testing

[Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) is set up as a pinned local .NET tool (`.config/dotnet-tools.json`). It checks that the test suite actually fails when the code is subtly broken, not just that it runs.

```powershell
dotnet tool restore
cd src/ManyWinters.Tests

# ManyWinters.Tests references more than one project, so tell Stryker which one to mutate:
dotnet tool run dotnet-stryker --project ManyWinters.Core.csproj
dotnet tool run dotnet-stryker --project ManyWinters.Tools.SimulationRunner.csproj

# The presentation layer has its own test project, and its own config: only the extracted
# calculations are mutated, since the rest of ManyWinters.Godot is untested engine wiring.
cd ../ManyWinters.Godot.Tests
dotnet tool run dotnet-stryker
```

Configuration lives in `src/ManyWinters.Tests/stryker-config.json`, and `src/ManyWinters.Godot.Tests/stryker-config.json` for the presentation layer — that second one mutates `**/Logic/*.cs` rather than `**/*.cs`, since mutating the untested engine wiring around it would bury the score. Putting an extracted calculation in that folder is therefore all it takes to have it mutated; there is no list to keep in step. The break threshold is currently **100%** — the codebase is small enough that every mutant should be killed; a survivor is either a real test gap (add a test) or a genuinely equivalent mutation (suppress it inline with `// Stryker disable once <Mutator>: <reason>` and explain why). Lower the threshold only as a deliberate, documented, temporary exception — never silently.

**Inputs where the operation cancels out prove nothing.** The most common reason a mutant survives here is not a missing test but a test fed tidy numbers. Three real examples: every ray in the first `BillboardUv` tests ran perpendicular to the billboard's plane, so getting the ray/plane crossing wrong only moved the point along the plane's own normal, which the UV ignores; every `EntityVisualVariation.RangeFor` test used a 0-to-1 range, where multiplying by the width, dividing by it, and using `max + min` all give the same answer; and every food in the content restores exactly 1 hunger per unit, where dividing by the rate and multiplying by it agree. A rate of 1, a range of 0 to 1, an axis-aligned ray, a symmetric loop - if the arithmetic cancels for the input you chose, the test passes whatever the code does. **Pick oblique, off-centre, asymmetric values.**

This is slow enough that it isn't part of the main `ci.yml` gate; it runs daily and on manual dispatch via `.github/workflows/mutation.yml`.

## Formatting

Whitespace formatting (indentation, line endings, spacing — whatever `.editorconfig` says) is a build error, not a suggestion: `Directory.Build.props` turns on `EnforceCodeStyleInBuild`, and `.editorconfig` sets the formatting rule IDE0055 to `error`, so a file that drifted — a tab-indented one saved from a tool that ignores `.editorconfig`, say — fails `dotnet build` right there instead of only failing CI's `dotnet format` step later. Fix it with:

```powershell
dotnet format ManyWinters.sln
```

`.gitattributes` pins every text file to LF on checkout regardless of the machine's `core.autocrlf`, so line endings can't drift either.

## Inspections

Roslyn analyzers run as part of every build (`Directory.Build.props`), but they only ever see one project at a time, so a public member nothing outside its type reads, a collection only ever written to, or a class nothing instantiates all pass them silently. [ReSharper InspectCode](https://www.jetbrains.com/help/resharper/InspectCode.html) — free, pinned as a local tool alongside Stryker — does solution-wide analysis and is what catches those. CI runs it after the build and fails on anything at warning severity or above.

```powershell
dotnet tool restore
dotnet build ManyWinters.sln
dotnet jb inspectcode ManyWinters.sln --swea --no-build --severity=WARNING -f=Text -o=-
```

Which inspections count is decided in `.editorconfig`: the dead-code family (`resharper_unused_member_global_highlighting` and friends) is raised to `warning` there, since it ships as mere suggestions; style suggestions stay below the gate. A genuine false positive — a Godot `[Export]` setter the engine writes, a JSON record the serializer instantiates — is silenced inline with `// ReSharper disable once <InspectionId>` and a comment saying why, never by lowering the inspection for everyone.

If the tool aborts with "MSBuild process was started ... but the IDE failed to connect to it" on Windows, it picked up a Visual Studio Build Tools MSBuild; point it at the SDK's instead, e.g. `--toolset-path="C:\Program Files\dotnet\sdk\8.0.424\MSBuild.dll"`.

## Development notes

- Keep simulation logic out of `ManyWinters.Godot` — the presentation layer only reads simulation state and sends commands (see the plan's "Commands Instead of Direct Manipulation" section). Never mutate simulation state directly from UI code.
- Prefer adding a headless test or `SimulationRunner` scenario over manually verifying behavior in the editor when possible — it's faster to iterate and easier to keep deterministic.
- See [`roadmap.md`](roadmap.md) before starting new work — it defines the current priority order and what is explicitly out of scope for now.
