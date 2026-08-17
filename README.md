# Of Folks and Many Winters

A real-time strategy game about the birth and long-term evolution of a single people — see [`docs/readme.md`](docs/readme.md) for the game concept, [`docs/of-folk-and-many-winters-plan.md`](docs/of-folk-and-many-winters-plan.md) and [`docs/Of Folk and Many Winters — Technical Implementation Plan.md`](<docs/Of Folk and Many Winters — Technical Implementation Plan.md>) for the design and technical plans, [`docs/roadmap.md`](docs/roadmap.md) for the current execution order, and [`docs/conventions.md`](docs/conventions.md) for general engineering conventions.

## Status

The project skeleton (`OfFolk.Core`, `OfFolk.Godot`, `OfFolk.Tools.SimulationRunner`, `OfFolk.Tests`) exists and builds, but contains no simulation logic yet — that's Step 2 of the roadmap.

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

Per the [technical implementation plan](<docs/Of Folk and Many Winters — Technical Implementation Plan.md>):

```text
src/
├── OfFolk.Core/               # Pure C# simulation — no Godot dependency, ever
├── OfFolk.Godot/              # Godot project: presentation, rendering, input, UI, audio
├── OfFolk.Tools/
│   └── SimulationRunner/      # Headless console runner (no Godot required)
└── OfFolk.Tests/              # Tests for OfFolk.Core
```

`OfFolk.Core` must never reference `OfFolk.Godot`. The simulation must be runnable and testable headlessly, without the engine.

## Building and running

```powershell
# Build everything
dotnet build

# Run the headless simulation runner (no Godot required)
dotnet run --project src/OfFolk.Tools/SimulationRunner

# Run tests
dotnet test
```

To run the game with rendering, open `src/OfFolk.Godot` in the Godot editor, or launch it directly:

```powershell
godot --path src/OfFolk.Godot
```

## Development notes

- Keep simulation logic out of `OfFolk.Godot` — the presentation layer only reads simulation state and sends commands (see the plan's "Commands Instead of Direct Manipulation" section). Never mutate simulation state directly from UI code.
- Prefer adding a headless test or `SimulationRunner` scenario over manually verifying behavior in the editor when possible — it's faster to iterate and easier to keep deterministic.
- See [`docs/roadmap.md`](docs/roadmap.md) before starting new work — it defines the current priority order and what is explicitly out of scope for now.
