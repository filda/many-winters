# Terrain and World-Scale Architecture

## Purpose

This pins down the coordinate system, terrain data source, and level-of-detail strategy before any terrain/camera code gets written, because it changes `Position` — a type touched by every entity in `ManyWinters.Core`. It supersedes the placeholder "1 km × 1 km, 1 m cell" note in [`Many Winters — visual and prototype plan for the agent.md`](<Many Winters — visual and prototype plan for the agent.md>) section 6, which was a rough guess, not a decision.

The long-term goal is real-world geography: the eventual playable map is all of Europe, built from actual elevation/hydrology data, not a stylized or procedurally invented landmass. Everything below exists to make that reachable without a rewrite, while keeping the first implementable slice small.

---

## 1. Coordinate reference system: ETRS89 / LAEA Europe (EPSG:3035)

An equal-area, meters-based projection purpose-built for pan-European datasets. It's what EU-DEM, EU-Hydro, and most EEA data already ship in, and it avoids the seams a multi-zone UTM approach would create across a continent-spanning map.

`Position` becomes `(double X, double Y)` in this CRS, in meters. Construction call sites don't need to change — `float` widens to `double` implicitly — but the field width changes, and so does the save format (see "Impact on existing code" below).

## 2. Precision: `double` in Core, `float` (floating origin) in Godot

EPSG:3035 coordinates across Europe run roughly X: 1.5–7.4 million, Y: 0.9–5.5 million meters. `float32`'s precision at that magnitude is on the order of tens of centimeters — good enough for nothing that needs sub-meter simulation.

- **Core** (`WorldState`, `Person.Position`, every entity) always stores true world-space coordinates as `double`. This is pure data — no Godot dependency — so it stays consistent with "Core never references Godot."
- **Godot** re-centers a `float`-space render origin around the active camera/settlement (a "floating origin") for rendering only. That re-centered value is never written back as simulation truth.

## 3. Terrain data source

Candidates to validate against licensing/practicality when actually fetching data — not locked in stone, but this is the shortlist:

- **Elevation**: EU-DEM v1.1 (EEA, 25 m, already in EPSG:3035) as the primary source; Copernicus DEM GLO-30 as a fallback for coverage EU-DEM lacks.
- **Hydrology / coastline**: EU-Hydro (EEA) for rivers; EU-Hydro or Natural Earth for the coastline.

## 4. Simulation truth vs. visual-only detail

- **Simulation truth** (Core, no Godot dependency): elevation samples at the DEM's native resolution (~25 m), exposed through a height-sampling abstraction (e.g. `ITerrainHeightSource`) that Core queries by `Position`. Tests inject a trivial flat implementation — nothing in the existing 200+ tests needs real terrain data, and none of them break from this.
- **Visual-only** (Godot layer): anything finer than the DEM's native resolution — including the "1 m" figure from the visual plan — is mesh/rendering embellishment, not new simulation-relevant ground truth. This resolves that document's ambiguity: 1 m is a rendering target, not a data resolution.

## 5. Level of detail / hierarchy

Real elevation data at 1 m over all of Europe would be roughly 10¹³ cells — not representable at once by any reasonable setup. So:

- **Tier 0 — continent overview**: the DEM downsampled far past native resolution (on the order of 100 m–1 km per sample), covering all of Europe, cheap enough to hold in memory at once. Drives macro decisions and far fog-of-war ("discovered" but not "visible").
- **Tier 1 — regional tiles**: DEM at/near native resolution (~25 m), loaded per region on demand.
- **Tier 2 — active area**: the immediate surroundings of a settlement, at full simulated detail (visual detail layered on top of Tier 1 data).

Only Tier 2 needs to exist for the first prototype. Tiers 0/1 are the seam that makes "eventually all of Europe" reachable without redesigning Tier 2's contract later.

## 6. First implementable slice

Not "all of Europe" — a real but small area:

- Pick one small (a few km²) real area with the relief the visual plan's pillars want (valley, river, forest), and import just that patch.
- Build the `Position` (`double`) / height-sampling abstraction generally, but wire up exactly one loaded tile.
- Explicitly deferred from this slice: continent-wide streaming/paging, procedural fill for areas without downloaded data, fog of war, movement/pathfinding, and the rest of the visual plan's asset/UI backlog.

## Impact on existing code

- `Position`: `float` → `double`. Existing construction call sites keep compiling unchanged. Save-data records (`PersonSaveData.PositionX/Y` and the equivalents on resource nodes, buildings, and graves) change type, bumping `SaveGameService.CurrentVersion` again.
- No behavior change to existing gameplay logic or tests — nothing today reads real-world coordinates meaningfully, so this is a type-width change, not a logic change.

---

## Explicitly out of scope here (tracked separately)

- **Movement/pathfinding.** Core has no movement system today (`Position` never changes on its own tick-over-tick). Needs its own planning pass before terrain can actually "move characters," which was the original motivation for this work.
- **Fog of war.** Naturally maps onto the tier boundaries above (visible = Tier 2, discovered = explored Tier 1 tiles, unknown = everything else) but isn't designed yet.
- **Camera projection** (orthographic vs. perspective). Left deliberately open — expected to be settled by experimentation against real terrain, not decided up front by a document.
