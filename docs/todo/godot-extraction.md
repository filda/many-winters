# Godot layer: what can be extracted and tested

Working backlog of what is *left*. Finished items are dropped rather than ticked off - git
history is the record. Every method worth splitting into a calculation and an engine wrapper
has now been split; what remains is one inconsistency between two of the results, and the list
of things deliberately not touched.

Scope: code inside `src/ManyWinters.Godot`, and only what could move or be split. General
refactoring lands in `refactoring.md`; anything whose answer belongs in Core is there too.

The rules this works under are not repeated here: `docs/conventions.md` has the principle
(calculation lives apart from the code the framework calls), and `docs/development.md`, under
"Testing the presentation layer", has what the engine actually permits, where an extracted
calculation belongs (`src/ManyWinters.Godot/Logic/`, which the mutation config globs), and -
under "Mutation testing" - why a tidy test input is the usual reason a mutant survives.

---

## One concept, two formulas

**"How big does this sprite render"** is still answered two ways. `BillboardUv.RenderedSize`
takes the sprite's own pixel size, texture size and per-axis node scale, and both pixel-accurate
picking and the occlusion fade now use it. `SpriteExtents.From` does not: it derives
metres-per-pixel from the `worldHeight` a sprite was *created* at, so it ignores node scale
entirely - and since hover scales a sprite by a tenth, every hovered thing is briefly out of
step with its own click rectangle.

Settling it means handing `SpriteVisibleExtent` the live sprite rather than a height, which
touches all six of its callers. Worth doing; not free, and not urgent while the discrepancy is
a tenth of a sprite's width for as long as the cursor rests on it.

## Deliberately left alone

`TerrainSetup`, `CloudScatter.Scatter`, `BillboardSprite.Create`, `GroundShadow.Create`,
`TextureCache`, `ContentFiles`, `CloudFogMask`, every `_Ready`/`_Process`/`OnInputEvent`, and
the `On*ButtonPressed` handlers in `Main` are wiring with no decision of their own - a test
would assert that the implementation is the implementation.
