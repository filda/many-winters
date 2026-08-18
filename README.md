# Of Folks and Many Winters

A real-time strategy game about the birth and long-term evolution of a single people — see [`docs/readme.md`](docs/readme.md) for the game concept, [`docs/of-folk-and-many-winters-plan.md`](docs/of-folk-and-many-winters-plan.md) and [`docs/Of Folk and Many Winters — Technical Implementation Plan.md`](<docs/Of Folk and Many Winters — Technical Implementation Plan.md>) for the design and technical plans, [`docs/roadmap.md`](docs/roadmap.md) for the current execution order, and [`docs/conventions.md`](docs/conventions.md) for general engineering conventions.

## Status

Progress against [`docs/roadmap.md`](docs/roadmap.md):

- **Step 2 — Simulation skeleton**: a simulation clock, stable person IDs, a minimal `Person`/`Needs`/task-queue model, and JSON-based save/load, all in `ManyWinters.Core` with no Godot dependency.
- **Step 3 — Headless runner**: `generate`, `create <n>`, `simulate <ticks>`, `print population`, `save`/`load` — runs the simulation with no Godot involved.
- **Step 4 — Godot presentation**: people render as placeholder capsules you can click to inspect; a "Spawn Person" button goes through a real `ICommand` (`SpawnPersonCommand`) instead of touching simulation state directly.
- **Step 5 — Environment and survival** (partial): hunger increases every tick and can kill a person; resource nodes (colored boxes, one of the resource kinds defined under `src/ManyWinters.Godot/Content/resources/`, e.g. apple/pear/mushroom/potato) can be gathered from — select a person, then click a node to issue a `GatherCommand` — and the node disappears once depleted. Terrain/weather/fire/shelter are intentionally deferred to their own later steps.
- **Step 7 — Skills and knowledge** (partial): resources, skills, and techniques are data-driven rather than compiled — each is defined by a JSON file (`src/ManyWinters.Godot/Content/{resources,skills}/<id>/<id>.json`) loaded at startup into a `ResourceCatalog`/`SkillCatalog`, keyed by string ids (`ResourceKindId`/`SkillTypeId`/`TechniqueId`). Each resource's definition names the skill it trains — apples and pears both train `foraging`, mushrooms and potatoes each train their own skill, so gathering-style skills don't have to be 1:1 with resource kinds. `Person.Skills` grows per skill with each gather action, discovering that skill's efficient technique after enough practice — bigger harvests afterward, and the discovery doesn't leak into other skills. Right-click one person onto another to `TeachCommand` them anything the first person knows. Knowledge lives in people, not a global tech tree, and it's all shown in the Godot info panel. A full prerequisite graph is still deferred; visual/asset data (`.tres`) alongside each resource/skill definition is also deferred.
- **Step 6 — Headless + visual milestone**: `SurvivalMilestoneTests` (`src/ManyWinters.Tests/Milestones/`) proves 10–20 people survive 300 ticks with regular gathering and starve within 150 ticks without it. Seasons/winter don't exist yet (Step 9), so this validates the underlying survival pressure rather than a literal winter. The Godot scene now spawns 15 people and 5 resource nodes to match.

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

Every run of the tool starts a fresh, empty world — nothing persists between separate invocations unless you explicitly `save`/`load` it. Chain as many commands as you want into a single invocation, unquoted:

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

## Mutation testing

[Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) is set up as a pinned local .NET tool (`.config/dotnet-tools.json`). It checks that the test suite actually fails when the code is subtly broken, not just that it runs.

```powershell
dotnet tool restore
cd src/ManyWinters.Tests

# ManyWinters.Tests references more than one project, so tell Stryker which one to mutate:
dotnet tool run dotnet-stryker --project ManyWinters.Core.csproj
dotnet tool run dotnet-stryker --project ManyWinters.Tools.SimulationRunner.csproj
```

Configuration lives in `src/ManyWinters.Tests/stryker-config.json`. The break threshold is currently **100%** — the codebase is small enough that every mutant should be killed; a survivor is either a real test gap (add a test) or a genuinely equivalent mutation (suppress it inline with `// Stryker disable once <Mutator>: <reason>` and explain why). Lower the threshold only as a deliberate, documented, temporary exception — never silently.

This is slow enough that it isn't part of the main `ci.yml` gate; it runs daily and on manual dispatch via `.github/workflows/mutation.yml`.

## Development notes

- Keep simulation logic out of `ManyWinters.Godot` — the presentation layer only reads simulation state and sends commands (see the plan's "Commands Instead of Direct Manipulation" section). Never mutate simulation state directly from UI code.
- Prefer adding a headless test or `SimulationRunner` scenario over manually verifying behavior in the editor when possible — it's faster to iterate and easier to keep deterministic.
- See [`docs/roadmap.md`](docs/roadmap.md) before starting new work — it defines the current priority order and what is explicitly out of scope for now.
