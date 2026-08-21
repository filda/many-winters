# Zeman-style sprite prompts

Reference: [`docs/ZemanConceptArt.png`](../docs/ZemanConceptArt.png), [`docs/ZemanSprites.png`](../docs/ZemanSprites.png).

These are prompts for an external AI image tool (Midjourney, Stable Diffusion, etc.) —
nothing here is called by the game or by any script. If a batch comes back looking right,
the images replace the corresponding files under `src/ManyWinters.Godot/Content/**` (same
naming as `art/generate_sprites.py` produces) and `WorldPresenter`/`BillboardSprite` will
render them exactly like the current procedural placeholders, no engine changes needed.

## Style block

Prepend this to every prompt below — it's what keeps the whole set looking like one
consistent world instead of thirteen unrelated images.

> 19th-century woodcut engraving illustration, in the style of Karel Zeman's *Baron
> Prášil* films — fine hand-inked crosshatching, etched linework, stage-set and
> paper-cutout diorama feeling. Muted earthy palette: ochre, sepia, slate blue-grey,
> olive green, rust brown, charcoal black. Stone-age / early Neolithic setting. Single
> consistent light source from the upper left. Flat, illustrative, not photorealistic —
> no painterly gradients, no glossy render, no 3D-CGI look.

## Negative / consistency guardrails

**Round 1 result:** asking for a "transparent background" got ignored — the tool (ChatGPT
image gen) rendered every subject on a dark radial vignette/glow instead, with the glow
blending softly into the subject's edges. That's not cleanly cuttable: a soft halo fringe
around every sprite would show up as a visible glow in-game. Two changes for the next
round:

1. Stop asking for "transparent" — ask for one **flat, solid, single-color background**
   instead (a chroma-key color the slicing script can threshold out deterministically).
   Plain white or pure magenta both work; avoid green given how much foliage/olive is in
   the palette itself.
2. Explicitly forbid glow/vignette/halo effects, since the tool defaults to adding one.

Append or set as a negative prompt for every asset:

> flat solid white background, no vignette, no glow, no radial light halo, no gradient
> background, no background scenery, no ground shadow baked into a rectangle, no paper
> texture behind the subject, no readable text or watermark, no modern clothing or
> objects, no color photograph look, centered, single subject, hard clean edge between
> subject and background

And separately, as production notes (not part of the prompt text itself):

- **The background must be one flat color, not a gradient or vignette.** These are
  billboard textures composited over real 3D terrain — any soft halo or baked-in shadow
  will show as a visible glow around the sprite in-game. A flat color is also what makes
  automated slicing/chroma-keying possible at all.
- **Keep scale/proportion consistent across the whole batch.** The game already relies on
  `EntityVisualVariation` for per-instance tint/scale jitter — the base art itself should
  be uniform (same head-to-body ratio, same framing distance) or nearby sprites will look
  mismatched in size next to each other.
- **Square canvas**, generous margin around the subject (the current pixel-art sprites are
  64×64 with the subject filling most of the frame — leave enough border that cropping
  to square doesn't clip limbs or foliage).
- Generate a few seeds per prompt and pick the cleanest silhouette — a billboard sprite
  lives or dies by whether its outline reads correctly at a distance, not by fine detail.

## Current asset roster (drop-in replacements)

Thirteen of these map 1:1 to `art/generate_sprites.py`'s `SPRITES` dict — same filenames,
same role in the game, straight reskins with no engine changes needed. Two (`axe`,
`warm_clothing`) are new: they're real craftable items (`CraftCommand`) that currently
have *no* visual at all — inventory only ever shows them as plain text
(`"{kind} x{count}"`). Generating art for them doesn't make them appear anywhere by
itself; showing an item icon in the inventory line is a separate, not-yet-built bit of UI
wiring. Worth having the art ready ahead of that, but don't expect them to show up
in-game just from dropping the file in.

Note that the sim has no profession/role system today — one person can gather, build, and
craft interchangeably. So `person` below is deliberately **neutral**, not locked to
"hunter" or "gatherer" the way the concept art's cast is. If a role system gets added
later, the *stretch* section below has the per-role prompts already broken out.

**`person`** (generic villager, front-facing, full body)
> A single early-Neolithic villager standing front-on, plain wrap tunic and cross-gartered
> leggings, simple leather boots, short hooded cloak, minimal tools. Neutral idle stance,
> arms loosely at sides. Full body visible head to feet.

**`person_dead`**
> The same villager lying on their side on the ground, fallen and still, cloak draped over
> the body, seen from a slightly elevated side angle. Muted, desaturated coloring — cold
> grey-blue tint rather than the living palette.

**`wood`** (log bundle resource node)
> A bundle of three cut logs stacked together, bark on the outside, pale cut rings visible
> on the exposed ends, tied with a rope. Ground-level object, no character.

**`apple`**
> A single ripe apple hanging from a short stem with one small leaf, deep red skin with a
> visible highlight. Ground-level resource icon, no character.

**`pear`**
> A single ripe pear on a short stem with one leaf, yellow-green skin with faint
> speckling. Ground-level resource icon, no character.

**`potato`**
> A small cluster of two or three unearthed potatoes, tan-brown skin with visible eyes/
> dimples, a little clinging soil at the base. Ground-level resource icon, no character.

**`mushroom`**
> A single wild mushroom, domed brown cap with faint pale speckles, pale gilled underside,
> short stem. Ground-level resource icon, no character.

**`axe`** *(new — currently a craftable item with no sprite at all; every other item kind
in the game has one, so this closes that gap)*
> A single stone-headed axe, wooden handle bound to a knapped stone head with leather
> cord wrapping, resting diagonally. Standalone tool icon, no character, no ground.

**`warm_clothing`** *(new — same gap as `axe`)*
> A single folded fur-trimmed cloak or wrap, thick hide with a fur collar, folded into a
> compact bundle as if held in inventory rather than worn. Standalone icon, no character.

**`storage_hut`**
> A small early-Neolithic storage hut: plank walls, steep thatched roof, one plank door,
> one small window. Seen head-on, three-quarter isometric lean. Standalone building, no
> characters or background scenery.

**`grave_unmarked`**
> A bare mound of disturbed dark earth with a few loose stones scattered on it, no marker,
> no headstone — nothing to read. Ground-level object.

**`grave_marked`**
> A small earth mound with a rough carved stone slab planted upright in it, simple
> engraved marks on the face of the stone (not legible text, just carved lines).
> Ground-level object.

**`conifer_tree`**
> A single tall conifer tree, three tiers of drooping branches, narrow silhouette, full
> tree visible from base to tip. Standalone, no background forest.

**`rock_pile`**
> A small pile of three or four rounded grey stones stacked together. Ground-level object.

**`selection_marker`** — **redo, round 2 result was wrong.** Generated inside the 15-asset
batch, it came back as a ring of stones (the model apparently latched onto the
grave/rock-pile context elsewhere in the same sheet instead of the actual description).
Send this one **on its own, in a fresh chat/context**, not batched with anything else —
that's the most likely fix:

> A single flat 2D emblem/icon, a simple downward-pointing chevron or arrow shape made of
> solid gold-ochre metal, engraved with fine linework detail matching a 19th-century
> woodcut illustration style. Not a physical 3D object, not a landscape element — no
> stones, no earth, no mound, no ground. Isolated icon on a flat solid white background,
> no vignette, no glow, no gradient, centered.

## Batch prompt (single image, all 15 current-roster assets)

Round 1 was generated as one sheet with all subjects laid out in a grid rather than
one call per asset — this is the same idea for round 2, with the background fix and the
two new items folded in. Copy-paste as one prompt:

> 19th-century woodcut engraving illustration, in the style of Karel Zeman's *Baron
> Prášil* films — fine hand-inked crosshatching, etched linework, stage-set and
> paper-cutout diorama feeling. Muted earthy palette: ochre, sepia, slate blue-grey,
> olive green, rust brown, charcoal black. Stone-age / early Neolithic setting. Single
> consistent light source from the upper left. Flat, illustrative, not photorealistic —
> no painterly gradients, no glossy render, no 3D-CGI look.
>
> Generate a grid sheet of 15 separate isolated objects, each clearly separated from its
> neighbors with visible empty margin, laid out in rows, no shared background scenery
> connecting them:
>
> 1. A neutral early-Neolithic villager standing front-on, plain wrap tunic and
>    cross-gartered leggings, simple leather boots, short hooded cloak, neutral idle
>    stance, full body head to feet.
> 2. The same villager lying on their side on the ground, fallen and still, cloak draped
>    over the body, desaturated cold grey-blue tint.
> 3. A bundle of three cut logs stacked together, bark outside, pale cut rings on the
>    exposed ends, tied with rope.
> 4. A single ripe apple hanging from a short stem with one leaf, deep red skin with a
>    highlight.
> 5. A single ripe pear on a short stem with one leaf, yellow-green skin with faint
>    speckling.
> 6. A small cluster of two or three unearthed potatoes, tan-brown skin, visible eyes,
>    a little clinging soil.
> 7. A single wild mushroom, domed brown cap with pale speckles, pale gilled underside.
> 8. A single stone-headed axe, wooden handle bound to a knapped stone head with leather
>    cord wrapping, resting diagonally.
> 9. A single folded fur-trimmed cloak, thick hide with a fur collar, folded into a
>    compact bundle.
> 10. A small early-Neolithic storage hut: plank walls, steep thatched roof, one plank
>     door, one small window, seen head-on with a three-quarter isometric lean.
> 11. A bare mound of disturbed dark earth with a few loose stones scattered on it, no
>     marker, nothing to read.
> 12. A small earth mound with a rough carved stone slab planted upright in it, simple
>     engraved (not legible) marks on its face.
> 13. A single tall conifer tree, three tiers of drooping branches, narrow silhouette,
>     full tree base to tip.
> 14. A small pile of three or four rounded grey stones stacked together.
> 15. A small downward-pointing carved rune or etched mark in a single gold-ochre tone,
>     a floating UI glyph rather than a physical object.
>
> Every object isolated on one flat solid white background, no vignette, no glow, no
> radial light halo, no gradient background, no background scenery, no shared ground
> shadow, no readable text or watermark, no modern clothing or objects, no color
> photograph look, hard clean edge between each subject and the background.

Once that sheet comes back looking right, ask as a **separate follow-up message** in the
same chat (this is a request to the assistant/code tool, not part of the image prompt
above):

> Crop this sheet into 15 separate image files, one per object in the order listed above,
> named `01.png` through `15.png`. Pack all 15 into a single zip file for download.

Whether the tool actually does clean per-object cropping this way depends on it having a
code-execution/file tool available (ChatGPT's does) — if the crops it makes are sloppy or
it ignores the request, that's fine, send me the one sheet image instead and I'll slice it
myself the same way I would have anyway.

## Ground texture (higher risk — needs real seamless tiling)

A different problem from every sprite above: `TerrainRenderer` maps `ground.png` across
the *entire* 1km×1km terrain patch at a **16-meter tile size**
(`TextureTileMeters` in `TerrainRenderer.cs`) — the texture repeats roughly 62×62 ≈ 3,900
times. Any visible seam reads as an obvious repeating "wallpaper" grid at that count,
which is a much less forgiving failure mode than a soft edge on an isolated sprite.
General-purpose image tools have no built-in guarantee of edge-to-edge tileability —
unlike the sprite sheet, don't assume this works on the first try.

Two more constraints specific to this one, from how it's actually used:

- The mesh multiplies the texture by a **per-vertex height tint** — dark olive-green
  (`LowColor`, low ground) fading to pale warm grey (`HighColor`, high ground). The base
  texture itself needs to stay fairly neutral/mid-value, or the tint won't read.
- The current procedural texture (`art/generate_terrain_texture.py`) is deliberately
  *sparse* — small hatch marks and speckles, not a fully-rendered illustration — because
  dense engraving-level detail would turn into visual noise (moiré) once tiled thousands
  of times at a distance. Ask for something restrained, not a miniature version of the
  character sprites' linework density.

Prompt:

> 19th-century woodcut engraving illustration texture, in the style of Karel Zeman's
> *Baron Prášil* films — sparse hand-inked hatch marks and speckles on a flat muted
> olive-brown ground, mid-value and fairly neutral in tone (not dark, not light, not
> saturated). Restrained and subtle, not a detailed illustration — texture density
> similar to aged paper grain or canvas weave, not linework. Seamlessly tileable,
> edge-to-edge repeating pattern with no visible seam, no vignette, no gradient, no
> lighting direction, no focal object, no character, flat overhead view.

**Validation before anything gets wired in:** once you have a candidate, send it to me —
I'll tile it into a 4×4 grid and eyeball the seams myself (the same manual check the
procedural script's own comments describe), and check the opposite edges actually match.
I won't swap `ground.png` in the repo unless that check passes.

**Fallback (agreed in advance):** if it doesn't tile cleanly and isn't worth fighting
further, we don't force it — instead we improve `generate_terrain_texture.py` itself so
its procedural output better matches whatever the finished sprite batch actually looks
like (recolor `base`/`dark`/`light` in that script to sample from the real sprite palette
rather than the current placeholder greens/browns, and possibly shape the hatch marks to
read more "engraved" than "random speckle"). That keeps the guaranteed-seamless
construction and just makes it match tonally.

## Stretch: role variants and poses (Phase C scope, not needed yet)

Only worth generating once the game actually has a reason to show different people
differently — either a profession/role concept, or per-action sprite swapping keyed off
what a person's current task is (`MoveTask` → walking, `GatherCommand` → foraging/
carrying, `ConstructCommand`/`RepairCommand` → building). Listed here so the prompt
groundwork exists when that's decided, not as a queued task.

**Gatherer (foraging pose)**
> An early-Neolithic woman wearing a headscarf and long skirt, crouched over a low bush,
> reaching to pick berries, a woven basket on her other arm.

**Hunter (idle / spear)**
> An early-Neolithic man in a plain tunic and cross-gartered leggings, standing, holding a
> wooden spear upright at his side.

**Hunter (bow-drawing pose)**
> The same hunter in a wide braced stance, bow drawn fully back, arrow nocked, aiming to
> the side.

**Builder (carrying pose)**
> An early-Neolithic man carrying a long wooden log/beam balanced on one shoulder,
> walking pose.

**Builder (working pose)**
> The same man kneeling, hammering a wooden peg or working a plank with a simple stone
> tool.

**Elder (idle)**
> An old man with a long grey beard, wrapped in a heavy fur-trimmed cloak, leaning on a
> wooden walking staff, standing still.

**Walk cycle frames** (any role): same character, four frames — both feet together,
left foot forward, both feet together, right foot forward — identical framing/scale
across all four so they can be flipped into a loop.

## Stretch: fog-of-war visibility tiers (Phase D scope, not needed yet)

The concept art's "Visibility Levels" panel (currently visible / remembered / unknown)
is exactly Phase D from the visual plan doc. If that phase gets picked up, the same
style block plus a fading/desaturating treatment (full color → sepia monochrome →
flat silhouette wash) is the direction to prompt for — but this is a rendering/shader
concern as much as an art-asset one, so it needs its own design pass rather than just
more prompts.
