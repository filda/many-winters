# Of Folks and Many Winters

A real-time strategy game about the birth and long-term evolution of a single people — see [`docs/readme.md`](docs/readme.md) for the game concept, [`docs/of-folk-and-many-winters-plan.md`](docs/of-folk-and-many-winters-plan.md) and [`docs/Of Folk and Many Winters — Technical Implementation Plan.md`](<docs/Of Folk and Many Winters — Technical Implementation Plan.md>) for the design and technical plans, [`docs/roadmap.md`](docs/roadmap.md) for the current execution order, and [`docs/conventions.md`](docs/conventions.md) for general engineering conventions.

## Status

The simulation skeleton (Step 2 of the roadmap) is in place: a simulation clock, stable person IDs, a minimal `Person`/`Needs`/task-queue model, and JSON-based save/load, all in `ManyWinters.Core` with no Godot dependency. Next up is Step 3 — a richer headless runner.

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
├── ManyWinters.Core/          # Pure C# simulation — no Godot dependency, ever
├── ManyWinters.Godot/         # Godot project: presentation, rendering, input, UI, audio
├── ManyWinters.Tools/
│   └── SimulationRunner/      # Headless console runner (no Godot required)
└── ManyWinters.Tests/         # Tests for ManyWinters.Core
```

`ManyWinters.Core` must never reference `ManyWinters.Godot`. The simulation must be runnable and testable headlessly, without the engine.

## Building and running

```powershell
# Build everything
dotnet build

# Run the headless simulation runner (no Godot required)
dotnet run --project src/ManyWinters.Tools/SimulationRunner

# Run tests
dotnet test
```

To run the game with rendering, open `src/ManyWinters.Godot` in the Godot editor, or launch it directly:

```powershell
godot --path src/ManyWinters.Godot
```

## Mutation testing

[Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) is set up as a pinned local .NET tool (`.config/dotnet-tools.json`). It checks that the test suite actually fails when the code is subtly broken, not just that it runs.

```powershell
dotnet tool restore
cd src/ManyWinters.Tests
dotnet tool run dotnet-stryker
```

Configuration lives in `src/ManyWinters.Tests/stryker-config.json`. The break threshold is currently **100%** — the codebase is small enough that every mutant should be killed; a survivor is either a real test gap (add a test) or a genuinely equivalent mutation (suppress it inline with `// Stryker disable once <Mutator>: <reason>` and explain why). Lower the threshold only as a deliberate, documented, temporary exception — never silently.

This is slow enough that it isn't part of the main `ci.yml` gate; it runs daily and on manual dispatch via `.github/workflows/mutation.yml`.

## Development notes

- Keep simulation logic out of `ManyWinters.Godot` — the presentation layer only reads simulation state and sends commands (see the plan's "Commands Instead of Direct Manipulation" section). Never mutate simulation state directly from UI code.
- Prefer adding a headless test or `SimulationRunner` scenario over manually verifying behavior in the editor when possible — it's faster to iterate and easier to keep deterministic.
- See [`docs/roadmap.md`](docs/roadmap.md) before starting new work — it defines the current priority order and what is explicitly out of scope for now.
