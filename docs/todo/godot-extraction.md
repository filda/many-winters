# Godot layer: what can be extracted and tested

Working backlog of what is *left*. Finished items are dropped rather than ticked off - git
history is the record. Ordered by cost.

The rules this works under are not repeated here: `docs/conventions.md` has the principle
(calculation lives apart from the code the framework calls), and `docs/development.md`, under
"Testing the presentation layer", has what the engine actually permits, where an extracted
calculation belongs, and why the Godot mutation config lists files one by one.

---

## Split a method into calculation plus an engine wrapper

**`FogOfWarRenderer`** — three pieces:

- building the sharp masks (the texel loop producing two `float[,]` plus the `bool[,]`),
- `DistanceToExploredMeters` — world to texel index to array lookup times metres-per-texel,
- **the texel-to-world mapping**, which is currently written *asymmetrically*: texel to world
  is `(((ty + 0.5f) / size) - 0.5f) * 2f * half` (half-texel offset), world to texel is
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

**`Main.ComputeOccludingSprites` / `UpdateOcclusionFade`** — deciding which sprites currently
stand between the camera and the selection. It operates on `Sprite3D`, so what comes out is
the test itself (a sprite's world rectangle against a screen point), leaving the iteration
behind. This also owns one of the three sprite-size formulas below.

**`PersonView`** — the position interpolation in `_Process`/`SetTargetPosition`, and the
walk-cycle phase.

**`WorldPresenter.ToVector3`** — trivial, but instance-bound: make it static and take the
height sampler as a parameter.

## Consolidate — these are duplications, not just untested code

**Minimum spacing over a spatial hash exists three times.** `TerrainRenderer.CellFor` +
`IsTooCloseToAnExistingDecoration` + `MarkOccupied` is line-for-line the same algorithm as
`CloudSpotScatter.IsTooClose` in Core (buckets, 3x3 neighbourhood, `< spacing`), and
`MapLoader` has its own variant for spacing people out. One shared type in Core replaces all
three and is covered already.

**Rejection sampling — "random candidate, reject if too close, give up after N attempts" —
exists three times too:** `Main.FindFreeSpawnPosition`, `Main.FindFreeBuildingPosition` and
`MapLoader.NextCrowdPosition`. The only Godot dependency is `GD.Randf()`; pass a `Random` and
the whole thing moves to Core. Spawn placement is arguably a rule of the world rather than
presentation anyway.

**"How big does this sprite render" is owned in three places.** `BillboardUv.RenderedSize`
(pixel size times texture size times per-axis scale) is the tested one, but
`Main.ComputeOccludingSprites` re-derives a radius from pixel size and width alone, ignoring
scale and assuming every billboard is square, and `SpriteVisibleExtent.Compute` re-derives
metres-per-pixel from a `worldHeight` instead of asking the sprite, also ignoring scale. So for
any scaled or non-square sprite - which `ResourceNodeView` does produce, and hover scaling
creates on any sprite - the occlusion radius and the click rectangle are computed by different
formulas. One owner would settle it.

## Deliberately left alone

`TerrainSetup`, `CloudScatter.Scatter`, `BillboardSprite.Create`, `GroundShadow.Create`,
`TextureCache`, `ContentFiles`, `CloudFogMask`, every `_Ready`/`_Process`/`OnInputEvent`, and
the `On*ButtonPressed` handlers in `Main` are wiring with no decision of their own - a test
would assert that the implementation is the implementation.

## A lesson worth carrying to the next one

Every ray in the first cut of `BillboardUvTests` ran perpendicular to the billboard's plane,
which quietly hid four mutants: get the ray/plane crossing wrong and the point only moves
along the plane's own normal, which the UV then ignores. A real pick ray is perpendicular only
dead-centre on screen. **Oblique, off-centre, asymmetric inputs are what pin a geometric
calculation** - the tidy axis-aligned case is the one that proves least. The texel-to-world
mapping above is the next place this will matter.
