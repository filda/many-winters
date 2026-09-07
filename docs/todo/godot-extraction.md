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

**`PersonView`** — the position interpolation in `_Process`/`SetTargetPosition`, and the
walk-cycle phase.

**`WorldPresenter.ToVector3`** — trivial, but instance-bound: make it static and take the
height sampler as a parameter.

## One concept, three formulas

**"How big does this sprite render"** is answered in two places now that
`Main.ComputeOccludingSprites` asks `BillboardUv.RenderedSize` like the picking does.
`SpriteExtents.From` is the remaining odd one out: it derives metres-per-pixel from the
`worldHeight` a sprite was created at rather than from the sprite itself, so it ignores node
scale, and hover scaling puts every sprite briefly out of step with its own click rectangle.
Settling it means giving `SpriteVisibleExtent` the live sprite instead of a height, which
touches every caller - worth doing, but not free.

## Deliberately left alone

`TerrainSetup`, `CloudScatter.Scatter`, `BillboardSprite.Create`, `GroundShadow.Create`,
`TextureCache`, `ContentFiles`, `CloudFogMask`, every `_Ready`/`_Process`/`OnInputEvent`, and
the `On*ButtonPressed` handlers in `Main` are wiring with no decision of their own - a test
would assert that the implementation is the implementation.
