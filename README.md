# Of Folks and Many Winters

A real-time strategy game about the birth and long-term evolution of a single people — see [`docs/readme.md`](docs/readme.md) for the game concept, [`docs/of-folk-and-many-winters-plan.md`](docs/of-folk-and-many-winters-plan.md) and [`docs/Of Folk and Many Winters — Technical Implementation Plan.md`](<docs/Of Folk and Many Winters — Technical Implementation Plan.md>) for the design and technical plans, [`docs/roadmap.md`](docs/roadmap.md) for the current execution order, and [`docs/conventions.md`](docs/conventions.md) for general engineering conventions.

## Status

Progress against [`docs/roadmap.md`](docs/roadmap.md):

- **Step 2 — Simulation skeleton**: a simulation clock, stable person IDs, a minimal `Person`/`Needs`/task-queue model, and JSON-based save/load, all in `ManyWinters.Core` with no Godot dependency. The task-queue (`PersonTaskQueue`/`PersonTask`) existed since this step but sat unused until now: `WorldState.Advance` drives each alive person's current task every tick, and `MoveTask` is the first real one — it nudges `Person.Position` toward a destination by a fixed speed per tick and completes on arrival, rather than teleporting. `MoveCommand` issues one, pre-empting whatever the person was doing (`PersonTaskQueue.Interrupt`) rather than queuing behind it. In Godot, clicking empty ground with a person selected issues the move; their sprite glides smoothly toward the new position every rendered frame (`PersonView`'s own interpolation, at whatever speed covers the actual distance the simulation moved them in exactly one tick) rather than snapping once per tick. Every action command with an actual spatial target — gather, construct, repair, deposit, withdraw, bury, teach — now requires the acting person be within `WorldState.MaxInteractionDistance` (a single shared constant, checked via `WorldState.Distance`) of it, silently no-op'ing otherwise just like every other invalid-command case already did; `craft` is the one exception, since it only touches a person's own inventory and has no location to be near. The Godot buttons that pick a target automatically (repair/deposit/withdraw/bury's "nearest") now say so explicitly ("The nearest building is too far away.") instead of silently doing nothing, and auto-picked build placement is kept within range by construction rather than by luck.
- **Step 3 — Headless runner**: `generate`, `create <n>`, `simulate <ticks>`, `print population`, `save`/`load` — runs the simulation with no Godot involved.
- **Step 4 — Godot presentation**: people render as placeholder capsules you can click to inspect; a "Spawn Person" button goes through a real `ICommand` (`SpawnPersonCommand`) instead of touching simulation state directly.
- **Step 5 — Environment and survival** (partial): hunger increases every tick and can kill a person; resource nodes (colored boxes, one of the resource kinds defined under `src/ManyWinters.Godot/Content/resources/`, e.g. apple/pear/mushroom/potato) can be gathered from — select a person, then click a node to issue a `GatherCommand` — and the node disappears once depleted. Each resource's box color comes from a `ResourceVisualDefinition.tres` sitting next to its `.json` definition, not from code. Weather/fire/shelter are intentionally deferred to their own later steps. Terrain is real and now wired into the live game (not just a separate sandbox — see [`docs/terrain-and-world-scale-architecture.md`](docs/terrain-and-world-scale-architecture.md) for the full design): a real elevation grid — one 1 km × 1 km, 25 m-cell patch of actual EU-DEM data centered on 50.114722°N 14.4675°E, fetched once via `art/fetch_terrain.py` (the same one-off-script role `generate_sprites.py` plays for pixel art) into a static `Content/terrain/praha-liben/heightmap.json` the game loads locally, never over the network — renders as a real triangulated, textured 3D mesh (`TerrainRenderer`, shared by `Main.cs` and the standalone `TerrainSandbox.tscn` art/camera playground), complete with real OpenStreetMap waterway geometry (`art/fetch_stream.py`) and a scattered tree/rock backdrop. `Position` went from `float` to `double` for this — every entity's coordinate is now real-world meters — with a `FreeCameraRig` (WASD/arrows pan, Q/E rotate, R/F zoom, `T` toggles orthographic/perspective — perspective is the default, settled by direct comparison) replacing the old fixed camera, and `MapLoader.LoadDefault`'s camp relocated onto dry ground away from the real waterway. `WorldPresenter` samples the real terrain height so every entity — people, resource nodes, buildings, graves — actually sits on the slope instead of floating at a flat `y = 0`. Terrain height still isn't *gameplay*-relevant (movement speed, sightlines) — that stays deliberately deferred until something actually needs it.
- **Step 7 — Skills and knowledge** (partial): resources, skills, and techniques are data-driven rather than compiled — each is defined by a JSON file (`src/ManyWinters.Godot/Content/{resources,skills}/<id>/<id>.json`) loaded at startup into a `ResourceCatalog`/`SkillCatalog`, keyed by string ids (`ResourceKindId`/`SkillTypeId`/`TechniqueId`). Each resource's definition names the skill it trains — apples and pears both train `foraging`, mushrooms and potatoes each train their own skill, so gathering-style skills don't have to be 1:1 with resource kinds. `Person.Skills` grows per skill with each gather action, discovering that skill's efficient technique after enough practice — bigger harvests afterward, and the discovery doesn't leak into other skills. Right-click one person onto another to `TeachCommand` them anything the first person knows. Knowledge lives in people, not a global tech tree, and it's all shown in the Godot info panel. A full prerequisite graph is still deferred.
- **Step 6 — Headless + visual milestone**: `SurvivalMilestoneTests` (`src/ManyWinters.Tests/Milestones/`) proves 10–20 people survive 300 ticks with regular gathering and starve within 150 ticks without it. Written before seasons existed; that 300-tick run now spans a full year including one winter (see Step 9), so it doubles as a basic winter-survival check. The Godot scene now spawns 15 people and 6 resource nodes to match.
- **Step 8 — Tools and construction**: item instances exist as an `Inventory` (`ManyWinters.Core.Items`) — a per-person one, keyed by `ItemKindId`. Gathering a resource whose definition sets `yieldsItem` (currently only `wood`) fills the inventory instead of relieving hunger; the existing food resources are unchanged. A `SkillDefinition` can name a `tool` item and a harvest bonus for it — carrying an `axe` while woodcutting harvests more per action, on top of the Step 7 technique bonus. Crafting is data-driven too: `RecipeCatalog` (`src/ManyWinters.Godot/Content/recipes/<id>/<id>.json`) maps an output item to its input item/amount, and `CraftCommand` consumes the input from a person's inventory to produce it — wired to a "Craft Axe" button in the Godot UI. Construction follows the same shape: `Building` is a `WorldState`-tracked entity (`ManyWinters.Core.Construction`), `BuildingCatalog` defines what a building costs to put up (currently one `storage_hut`, 20 wood), and `ConstructCommand` consumes the materials and adds the building — wired to a "Build Storage Hut" button. Adding this whole new entity type only touched `WorldPresenter` (a `BuildingAdded` event + a `BuildingView`), not `Main`, validating the Step-8-detour presenter refactor. `Building.Condition` decays slowly every tick and `RepairCommand` restores it for a quarter of the build cost, wired to a "Repair Nearest Building" button. A `Building` also has its own `Inventory` — the same type a `Person` uses — so `storage_hut` is a genuine shared store rather than just a name: `DepositCommand`/`WithdrawCommand` move items between a person and the nearest building, wired to "Deposit Wood"/"Withdraw Wood" buttons, letting people pool resources instead of only using what they personally gathered. Entities also get deterministic per-instance tint/scale variation (`EntityVisualVariation`, seeded by entity id) so repeats of the same kind don't render as identical clones. Visuals themselves are billboard sprites (`BillboardSprite`) with real pixel art under `Content/{people,resources,buildings}/`, generated by `art/generate_sprites.py`.
- **Step 9 — Seasons and first winter** (partial): `WorldState.CurrentSeason` cycles Spring→Summer→Autumn→Winter derived from tick count (75 ticks/season, shown in the Godot UI next to the tick counter) — no extra state to save/load since it's computed straight from the clock. Gameplay effects (hunger cost, resource yield) never key off the `Season` name directly, though — that would hardcode "Winter is harsh," which doesn't generalize (a southern hemisphere would have it backwards). Instead, a `SeasonParameters` maps `Season → Climate` (`Cold`/`Mild`/`Hot`) and `Climate → hunger multiplier` / `Climate → regen multiplier`; swapping which season is `Cold` is a different `SeasonParameters` instance, not a code change, and there's no `if (climate == ...)` branch anywhere — both hunger accrual and resource regrowth are just `baseRate * multiplierForClimate`, uniformly, every tick. Each `ResourceDefinition` similarly carries a `ClimateYields` list — per-climate yield multipliers — so a resource can thrive in one climate and struggle in another (the four food resources drop to 40% yield in `Cold`, `wood` is unaffected) without any resource-specific branching in `GatherCommand`. `ResourceCatalog`/`SkillCatalog`/`RecipeCatalog`/`BuildingCatalog`/`ItemCatalog`/`SeasonParameters` now bundle into one `WorldConfiguration` passed into `WorldState`, replacing what had grown into four separate constructor overloads. Resource nodes also regrow — each `ResourceDefinition` can set a `RegenPerTick`, capped at the amount a node was originally spawned with (`MaxAmount`), scaled to zero in `Cold` via the regen multiplier (default calendar) rather than a special-cased pause — so a node survives being harvested across multiple winters instead of only ever depleting. Clothing/insulation closes the loop: a new `ItemCatalog` (`Content/items/<id>/<id>.json`) gives items metadata beyond being bare inventory keys — `warm_clothing` (crafted from 10 wood, same shape as the axe) carries an `Insulation` value that reduces a person's *effective* hunger multiplier (`Max(1, baseMultiplier - insulation)`, floored at the normal rate, never a bonus) — so surviving winter well is a product of prior preparation, not just luck. `WinterSurvivalMilestoneTests` proves winter's pressure is real (stopping gathering right as it begins is fatal within it) as distinct from Step 6's "never gather at all" baseline. Actual per-location climate (hemispheres, biomes) needs a concept of place that doesn't exist yet, so it's deferred alongside weather severity.
- **Step 10 — Life, death, and continuity**: a `Person` now has a `BirthTick`, and `WorldState.AgeInYears`/`AgeInSeasons` derive their age from it the same way `CurrentSeason` derives from the clock (10 years — `MaxLifespanYears` — is deliberately short given the game's 300-tick year, so old age is observable within a normal play/test session); dying of old age sits alongside the existing hunger death, both recorded via `DeathTick` and now a `CauseOfDeath` (`Hunger`/`OldAge`, old age taking priority if both conditions are met on the same tick). The inspector shows age in whole winters once someone's had their first one, or in seasons before that (matching the game's own title). A `Person` can also carry a `MotherId`/`FatherId` — there's no reproduction command yet, so today the only family ties that exist are the ones `MapLoader.LoadDefault` hand-assigns among the starting band (three couples, two children each). Dying doesn't move a person's possessions anywhere — they just stay in the corpse's own `Inventory`, lying with the body at its position rather than teleporting anywhere. `LootCommand` — proximity-gated exactly like burial — is how anyone living, not specifically a relative, walks up and physically takes them; there's no rightful-heir check, so possessions go to whoever gets there first, matching how a body's belongings work physically rather than by legal succession. Burial itself is *not* an automatic side effect of death — a deceased person just lies there, unburied, until someone deliberately buries them. Burial reuses the Step 7 skill/technique mechanic: a `burial` skill (`Content/skills/burial/burial.json`) grows with practice via `BuryCommand` and eventually discovers `efficient_burial`; burying without that technique produces an anonymous, unmarked `Grave` (no name, no age, no cause of death, no parents, no preserved knowledge — the link to who the person was is genuinely severed, not just hidden), while burying with it produces a fully recorded one, now also naming the cause of death and (when known) the deceased's mother and father — snapshotted as plain names at burial time, not live references, so the record stays meaningful even after a named parent is later buried too. Wired to a "Bury Nearest Dead Person" button and a "Loot Nearest Dead Person" button, a live grave count, and a clickable `GraveView` billboard (tinted differently for marked vs. unmarked, `res://Content/graves/grave_{marked,unmarked}.png` — no art drawn yet, so it falls back to a flat tinted quad like every other kind did before its sprites existed) that swaps the inspector panel over to that grave's record (or "Unmarked grave - no record survives" for anonymous ones).

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

If Godot reports that the C# project needs to be built (e.g. right after cloning, or after pulling changes), build it and try again:

```powershell
dotnet build src/ManyWinters.Godot
```

`dotnet build` writes straight into the assembly Godot loads (`src/ManyWinters.Godot/.godot/mono/temp/bin/`), so no separate editor-side build step is needed.

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
