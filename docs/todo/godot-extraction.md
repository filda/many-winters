# Godot layer: what can be extracted and tested

Working backlog of what is *left*. Finished items are dropped rather than ticked off - git
history is the record. The sections are ordered by cost, the items inside them by value, so the
first entry under each heading is the one worth doing next.

Scope: code inside `src/ManyWinters.Godot`, and only what could move or be split. General
refactoring lands in `refactoring.md`; anything whose answer belongs in Core is there too.

The rules this works under are not repeated here: `docs/conventions.md` has the principle
(calculation lives apart from the code the framework calls), and `docs/development.md`, under
"Testing the presentation layer", has what the engine actually permits, where an extracted
calculation belongs (`src/ManyWinters.Godot/Logic/`, which the mutation config globs), and -
under "Mutation testing" - why a tidy test input is the usual reason a mutant survives.

---

## Split a method into calculation plus an engine wrapper

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

## One concept, three formulas

**"How big does this sprite render"** is answered in three places. `BillboardUv.RenderedSize`
(pixel size times texture size times per-axis scale) is the tested one, but
`Main.ComputeOccludingSprites` re-derives a radius from pixel size and width alone, ignoring
scale and assuming every billboard is square, and `SpriteExtents.From` derives
metres-per-pixel from a `worldHeight` instead of asking the sprite, also ignoring scale. So for
any scaled or non-square sprite - which `ResourceNodeView` does produce, and hover scaling
creates on any sprite - the occlusion radius and the click rectangle come out of different
formulas. One owner would settle it, and it is a prerequisite for the two split items above
that touch the same quantity.

## Deliberately left alone

`TerrainSetup`, `CloudScatter.Scatter`, `BillboardSprite.Create`, `GroundShadow.Create`,
`TextureCache`, `ContentFiles`, `CloudFogMask`, every `_Ready`/`_Process`/`OnInputEvent`, and
the `On*ButtonPressed` handlers in `Main` are wiring with no decision of their own - a test
would assert that the implementation is the implementation.
