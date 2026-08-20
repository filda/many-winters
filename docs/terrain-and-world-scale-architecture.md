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

- **Elevation**: EU-DEM v1.1 (EEA, 25 m, already in EPSG:3035), queried via the public OpenTopoData API (`api.opentopodata.org`, dataset `eudem25m`) rather than downloading/parsing a raw GeoTIFF — a pragmatic substitution for the first patch; a bulk pipeline is still open if/when more patches are needed.
- **Hydrology**: not EU-Hydro after all — real waterway centerlines (name, type, width) come from OpenStreetMap via the public Overpass API (`overpass-api.de`), which turned out to have exactly what was needed (including the actual named river running through the first patch) without needing a GIS toolchain. EU-Hydro/coastline sourcing remains open for whenever coastal patches matter.
- Both are one-off fetches (`art/fetch_terrain.py`, `art/fetch_stream.py`) writing static JSON the game loads locally — never a runtime network call.

## 4. Simulation truth vs. visual-only detail

- **Currently visual-only, by choice**: elevation sampling (`TerrainRenderer.SampleHeight` in `ManyWinters.Godot`) stays entirely in the Godot layer for now. `ManyWinters.Core`'s simulation still only reasons in flat `(X, Y)` — nothing in it queries height. This deviates from this document's original framing (a Core-side `ITerrainHeightSource` "that Core queries"): that abstraction would be speculative with zero consumers today, which the project's own conventions argue against. It gets added *when* something real needs it (e.g. slope-affected movement speed), not ahead of that need.
- **Visual-only, by necessity**: anything finer than the DEM's native resolution — including the "1 m" figure from the visual plan — is mesh/rendering embellishment, not new simulation-relevant ground truth. This resolves that document's ambiguity: 1 m is a rendering target, not a data resolution.

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

- `Position`: `float` → `double`. **Done.** Existing construction call sites kept compiling unchanged, as predicted; all 260 tests passed without modification. Save-data records (`PersonSaveData.PositionX/Y` and the equivalents on resource nodes, buildings, and graves) changed type, bumping `SaveGameService.CurrentVersion` to 11.
- No behavior change to existing gameplay logic or tests — nothing reads real-world coordinates meaningfully yet, so this was a type-width change, not a logic change.
- One thing this document didn't anticipate: the real EPSG:3035 *absolute* coordinate switch (multi-million-meter values) is still deferred. `Position` values today are local meters relative to the one loaded patch's own center — small numbers, safe to cast to `float` for rendering without a floating-origin conversion. That conversion becomes necessary once a second patch/tile needs to coexist with the first; until then it would be speculative.

---

## Decided since this document was written

- **Camera projection: perspective.** Deliberately left open here to be settled by experimentation once real terrain existed (`TerrainSandbox.cs`) rather than decided up front. Both were implemented and compared directly against the real elevation patch (toggle key `T`); perspective — ordinary 3D foreshortening — was kept as the default. Orthographic stays available (in both `TerrainSandbox.cs` and the live game) for future comparison.
- **Movement.** `PersonTaskQueue`/`MoveTask`/`MoveCommand` drive `Person.Position` over real ticks in `ManyWinters.Core` (see the README's Step 2 entry).
- **Terrain wired into the live game.** `Main.tscn` now renders the same real elevation/water/decoration `TerrainRenderer` builds for `TerrainSandbox.tscn` (both share that class, plus `FreeCameraRig` for camera controls), replacing the old flat 20×20 test plane. `MapLoader.LoadDefault`'s camp was relocated onto dry ground away from the real waterway. `WorldPresenter` samples real terrain height when placing every entity, via a height-sampling delegate passed in from `Main.cs` — Core itself still has no height concept (see section 4).

## Still out of scope here (tracked separately)

- **Fog of war.** Naturally maps onto the tier boundaries above (visible = Tier 2, discovered = explored Tier 1 tiles, unknown = everything else) but isn't designed yet.
- **Terrain height affecting gameplay** (movement speed on slopes, sightlines). See section 4 — deliberately not added until something real needs it.
- **Continent-scale coordinates / floating origin.** See "Impact on existing code" above.
