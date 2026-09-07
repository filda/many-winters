# Godot layer: what can be extracted and tested

Working backlog, ordered by cost. Group A is done; B and C are not started.

## What is actually possible (verified, not assumed)

Probed with a scratch xunit project referencing `ManyWinters.Godot`:

- **Godot's math value types are plain managed structs** — `Vector2`/`Vector3`/`Basis`/
  `Transform3D`/`Color`/`Mathf`, including `Color.FromHsv`. A normal test project can
  reference the Godot project and call any static method that only uses those.
- **Anything `Node`/`Resource`-derived aborts the test host.** `new Node3D()` does not throw a
  catchable exception - the process dies. One such test takes the whole run down with it.
- **Mocking cannot cross that line.** `new Mock<Node3D>()` fails with
  `AccessViolationException`: a mock of a *class* is a generated subclass, so its constructor
  must call `Node3D()`, which enters the uninitialised native runtime. Godot node types are
  classes, not interfaces, so no mocking library helps.
- Watch for **indirect** engine access: a method can look pure and still call
  `ResourceLoader.Exists` or `ContentFiles` underneath. Two candidates were dropped from group
  A for exactly this.

So the route is extraction, which this repo has now taken six times: `CloudSpotScatter`,
`GroundCloudCoverage`, `ExplorationState`, `Noise2D`, `GridDistanceField` and `BoxBlur` are all
presentation logic living in Core with full mutation coverage and no test doubles.

**Where extracted code goes.** Core must never reference Godot (roadmap step 1), so anything
whose signature mentions `Color`, `Vector3` or an `Extent` stays in the Godot project and is
tested from `ManyWinters.Godot.Tests`. Only genuinely engine-free logic moves to Core.

What is left worth pulling out sits mostly in `Main`, `TerrainRenderer`, `ResourceNodeView`,
`FreeCameraRig`, `PersonView`, `WorldPresenter`, `FogOfWarRenderer` and `SpritePixelHit` - the
named items below, rather than a line count that would be wrong again after the next extraction.

**Going forward this backlog should not need to grow.** `docs/conventions.md` now asks for
calculation to be written apart from the code the framework calls in the first place, so new
work lands testable instead of arriving here. What is listed below is the code that predates
that.

---

## A. Already-pure statics — extracted (done)

Each moved into a file of its own rather than being made `internal` where it sat: the Stryker
config below mutates by file, and a small purpose-named file is also the honest home for a
calculation that a large view class was only borrowing.

| Was | Now |
| --- | --- |
| `FogOfWarRenderer.BoxBlur` | `ManyWinters.Core.World.BoxBlur.Blur` — sibling of `GridDistanceField`; lost its redundant `size` parameter, which could disagree with the array it described |
| `ResourceNodeView.CombineExtents` | `SpriteExtents.Combine` |
| `ResourceNodeView.InsertBeforeExtension` | `TexturePaths.InsertBeforeExtension` |
| `ResourceNodeView.VariantSuffixed` | `TexturePaths.VariantSuffixed` |
| `BuildingView.TexturePathFor` | `TexturePaths.ForBuilding` |
| `PersonView.ModulateFor` | `SpriteTint.ModulateFor` (the neutral base colour moved with it) |
| `Main.TaskText` | `InspectorText.ForTask` |
| `Main.GraveText` | `InspectorText.ForGrave` |
| `Main.ParentsText` | `InspectorText.ForParents` |

`SpritePixelHit.TryGetUv` came out too, as `BillboardUv.At` - a split rather than a move (the
camera calls stay behind in a wrapper), so it is really a group B item done early. It was worth
doing first: the billboard plane's basis and the ray/plane intersection are where a wrong sign
or axis shows up as hover that intermittently misses, which is miserable to diagnose from the
symptom alone.

Everything but the blur stays in the Godot project - `Color`, `Extent` and `res://` paths
cannot cross into Core - and is `internal`, reached through `InternalsVisibleTo`, so the
public API did not widen.

Dropped from this group after checking what they actually call:

- `ResourceNodeView.BaseTexturePathFor` — calls `HasTreeSprite` → `ResourceLoader.Exists`.
- `BuildingView.ColorFor` — loads a `.tres` through `ResourceLoader`.
- `WorldPresenter.ToVector3` — instance method depending on `_sampleHeight`; belongs in B.

## B. Split a method into calculation plus an engine wrapper

**`FogOfWarRenderer`**, three pieces left now that the blur has gone:

- building the sharp masks (the texel loop producing two `float[,]` plus the `bool[,]`),
- `DistanceToExploredMeters` — world to texel index to array lookup times metres-per-texel,
- **the texel↔world mapping**, which is currently written *asymmetrically*: texel to world is
  `(((ty + 0.5f) / size) - 0.5f) * 2f * half` (half-texel offset), world to texel is
  `((x / (2f * half)) + 0.5f) * size` floored (no offset). They are inverses but do not look
  like it. Extract as a pair and assert the round trip.

**`FreeCameraRig`** — the zoom step (`Pow(rate, direction * notch)` then
`Clamp(current * factor, min, max)`), `CameraDirection()` (tilt degrees to unit vector), and
the ground-clearance clamp inside `UpdateCamera`.

**`SpriteVisibleExtent.Compute`** — the engine reads `GetUsedRect` off the image, but turning
used-rect plus canvas size plus `worldHeight` into an `Extent` is pure. It decides collision
shape sizes and anchor points.

**`TerrainRenderer.SampleHeight` / `SampleRawHeight` / `FineVertexHeight` / `TerrainBump`** —
bilinear sampling over a `float[]` plus `Noise2D` (already in Core). Needs a small `Heightmap`
type extracted first; `TerrainRenderer` keeps the `res://` loading.

**`PersonView`** — the position interpolation in `_Process`/`SetTargetPosition`, and the
walk-cycle phase.

**`WorldPresenter.ToVector3`** — trivial, but instance-bound: make it static and take the
height sampler as a parameter.

## C. Consolidate — these are duplications, not just untested code

**Minimum spacing over a spatial hash exists three times.** `TerrainRenderer.CellFor` +
`IsTooCloseToAnExistingDecoration` + `MarkOccupied` is line-for-line the same algorithm as
`CloudSpotScatter.IsTooClose` in Core (buckets, 3×3 neighbourhood, `< spacing`), and
`MapLoader` has its own variant for spacing people out. One shared type in Core replaces all
three and is covered already.

**Rejection sampling — "random candidate, reject if too close, give up after N attempts" —
exists three times too:** `Main.FindFreeSpawnPosition`, `Main.FindFreeBuildingPosition` and
`MapLoader.NextCrowdPosition`. The only Godot dependency is `GD.Randf()`; pass a `Random` and
the whole thing moves to Core. Spawn placement is arguably a rule of the world rather than
presentation anyway.

## Deliberately left alone

`TerrainSetup`, `CloudScatter.Scatter`, `BillboardSprite.Create`, `GroundShadow.Create`,
`TextureCache`, `ContentFiles`, `CloudFogMask`, every `_Ready`/`_Process`/`OnInputEvent`, and
`Main`'s `On*ButtonPressed` handlers are wiring with no decision of their own - a test would
assert that the implementation is the implementation. `Main.ComputeOccludingSprites` and
`UpdateOcclusionFade` do hold logic, but operate directly on `Sprite3D`, so extracting them
would leave a wrapper around a wrapper.

Testing whether the wiring itself is right (does the view add the right children, does the
signal connect) needs a Godot-hosted runner such as gdUnit4 or GoDotTest, with a Godot binary
and a headless display in CI. Not worth it while the wiring is not producing bugs - and note
that mocks would not have covered this either.

## A lesson from testing the geometry

Every ray in the first cut of `BillboardUvTests` ran perpendicular to the billboard's plane,
which quietly hid four mutants: get the ray/plane crossing wrong and the point only moves
along the plane's own normal, which the UV then ignores. A real pick ray is perpendicular only
dead-centre on screen. **Oblique inputs are the ones that pin an intersection** - the tidy
axis-aligned case is the one that proves least.

## Mutation testing

`ManyWinters.Godot.Tests` is a Stryker target, added to `mutation.yml` alongside Core and the
SimulationRunner, at a 100% break threshold like the others.

It carries **its own `stryker-config.json`** whose `mutate` list names the extracted files one
by one rather than the usual `**/*.cs`. That is deliberate: the rest of `ManyWinters.Godot` is
engine wiring with no tests, so mutating all of it would bury the score under uncovered
mutants and say nothing about the code that *is* covered. **Anything extracted from group B or
C has to be added to that list**, or it is silently unmutated.
