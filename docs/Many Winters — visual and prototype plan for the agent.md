# Many Winters — Visual and Prototype Plan for the Agent

## Purpose
The purpose of this document is to translate the visual exploration done so far into a concrete brief for an implementation / production agent. This is not yet a final GDD, but a working plan that can be used to start building the first functional visual and gameplay prototype.

---

## 1. Creative direction

### High concept
**Many Winters** is a stylized realtime strategy / simulation game set across generations, beginning at the dawn of humanity in an early, prehistoric-feeling landscape. How far the timeline progresses from there is open-ended, not capped at any particular era. The player leads a small group of people who survive, learn, build a camp, and gradually settle the land.

### Visual identity
The resulting style should combine:

- **2.5D isometric / axonometric perspective**
- **genuinely shaped terrain** with hills, valleys, cliffs, and rivers
- **paper-like / cut-out objects and characters** that look glued onto the landscape
- **dark, grimy, illustrated look**
- **hand-drawn art, inked contours, a muted palette, paper texture**
- a light inspiration from the poetics of **Karel Zeman / paper cutouts / old illustrations**, but in a more practical, gameplay-usable form

### Mood
- melancholic
- raw
- earthy
- quiet
- unassuming
- human
- historically indeterminate, yet believable

---

## 2. Confirmed design decisions

### Camera
Preferred model:

- **free camera movement** across the map
- **zoom in/out**
- ability to **rotate the camera** around the vertical axis
- fixed or near-fixed tilt
- fog of war limits knowledge of the world, not camera movement

Orthographic vs. perspective projection was *not* decided by this document — see section 11's open question. It was settled by experimenting against real terrain in `TerrainSandbox.cs` (toggle key `T`): **perspective** is the default, kept for its ordinary 3D foreshortening; orthographic remains available in the sandbox for future comparison.

### Fog of war
Use **3 levels of visibility**:

1. **Visible**
   the area is currently within characters' sight, showing its current state

2. **Discovered**
   the area was discovered in the past, the terrain is known, but dynamic information may be outdated

3. **Unknown**
   the area is not yet known, hidden

### Object representation
Objects will be divided into 2 main types:

#### A. Terrain / world
- genuinely three-dimensional terrain
- rocks, slopes, streams, elevation differences
- the terrain must allow **occlusion behind hills**

#### B. Cut-out objects
- trees
- bushes
- rocks
- fences
- tents
- buildings
- small props

These objects should feel like **cutout / billboard elements**, meaning they:
- visually "float" above the terrain
- rotate to face the camera
- do not pretend to be full 3D models
- their stylization is part of the visual language

### Characters and animals
- characters and animals can have just **one side variant + flip**
- **left / right orientation** is sufficient for now
- full 8-directional handling is not necessary
- silhouette, readability, and role matter more than anatomical accuracy

---

## 3. Visual pillars

1. **The landscape is readable and dramatic**
   - elevation differences must be immediately apparent
   - the river, ford, cliff, forest, and camp must form a readable composition

2. **Objects are simple, but distinctive**
   - no hyper-detail
   - readable silhouette
   - hand-drawn art and textures

3. **Characters are small, but recognizable**
   - gatherer, hunter, builder, elder must be readable even from a distance
   - roles must be identifiable by pose and props

4. **UI must not break the atmosphere**
   - UI should feel more like an old panel / parchment / wooden or metal frames
   - functional, but not sterile-modern

5. **The world should feel like an animated illustration**
   - not a realistic 3D world
   - not classic pixel art
   - not a glossy RTS

---

## 4. What already works visually

Based on the concepts, the strongest directions appear to be:

- **a camp by a river in a mountain / forest valley**
- **a prominent cliff / ridge in the foreground or background**
- **a small number of characters in different roles**
- **a dark, muted palette**
- **paper tents, fences, campfires, wood, bushes**
- **animals as part of the ambient life**
- **winter as a naturally strong second variant**
- **a gameplay mockup with fog of war and simple UI**

---

## 5. What the agent should deliver in the first iteration

### Goal of the first iteration
Build a **clickable visual prototype** that verifies:

- that the style works in motion
- that camera movement and rotation do not break the illusion of the paper world
- that terrain, occlusion, and fog of war work together
- that even a small settlement is readable

### Deliverables
The agent should prepare the following outputs:

#### 1. Gameplay mockup
A scene similar to an "RTS screenshot," containing:
- a small camp
- 4–6 characters
- a river or stream
- forest and rocks
- fog of war
- simple UI
- selection of a single unit

#### 2. Winter variant
The same type of scene, but with:
- snow
- winter vegetation
- a cooler palette
- tracks / footprints / icy riverbanks

#### 3. First small settlement
Not just a camp, but the first seed of a settlement:
- multiple buildings
- a simple fence / enclosure
- a drying rack
- wood
- a campfire
- work activities

#### 4. Asset / style sheet
A reference overview of the core assets:
- character roles
- 5–10 environmental assets
- color palette
- material notes
- an example of a terrain tile / diorama

---

## 6. Technical prototype — scope

### World representation
The coordinate system, terrain data source, and level-of-detail hierarchy are decided in [`terrain-and-world-scale-architecture.md`](terrain-and-world-scale-architecture.md), not here — that document supersedes the "1 km × 1 km, 1 m cell" figure this section used to have. In short: the long-term target is real elevation/hydrology data for all of Europe, reached through a tiered level of detail (continent overview → regional tiles → one fully-detailed active area), and the first prototype only needs that last tier — one small, real patch of terrain.

### Rendering approach
Preferred approach:

- **3D terrain mesh**
- **orthographic / isometric-like camera**
- **billboard / camera-facing cutout assets**
- simple shadows / contact shadows, if they help ground the objects

### Terrain goals
It must be possible to verify:

- line of sight across actual relief
- occlusion by terrain
- readability of elevation
- traversability across slopes
- composition during zoom and rotation

---

## 7. Implementation tasks for the agent

## Phase A — visual sandbox foundation
1. create a simple terrain chunk
2. add a basic terrain material
3. add a watercourse / stream
4. populate the scene with a few rocks and trees
5. add a camera:
   - pan
   - zoom
   - rotation
6. verify how silhouette / cutout rendering looks during camera movement

## Phase B — object system
1. prepare a billboard system for:
   - trees
   - bushes
   - fences
   - tents
   - rocks
2. separate:
   - **visual representation**
   - **simulation footprint**
3. verify that objects visually sit correctly on slopes

## Phase C — characters
1. introduce at least 4 archetypes:
   - gatherer
   - hunter
   - builder
   - elder
2. use:
   - idle
   - walk
   - work pose
3. handle left / right orientation via flip

## Phase D — fog of war
1. implement 3-state visibility
2. distinguish:
   - visible
   - discovered
   - unknown
3. the overlay must fit stylistically with the visual direction

## Phase E — UI mockup
1. top resource bar
2. unit info panel
3. basic action buttons
4. time / time of day
5. minimal interaction:
   - character selection
   - simple task highlight

---

## 8. Asset backlog — priority

### Priority 1
- gatherer
- hunter
- builder
- elder
- hide tent
- campfire
- berry bush
- conifer tree
- rock pile
- log bundle
- rough fence
- stream edge set
- deer
- rabbit

### Priority 2
- drying rack
- storage basket
- small woodpile
- crude hut
- simple enclosure / pen
- hide-working station
- fishing pose / fishing prop
- skin / cloth strips
- path decals
- winter variants

### Priority 3
- first permanent buildings
- carts / primitive transport
- field / cultivation hints
- burial / ritual props
- cultural markers
- seasonal foliage variants

---

## 9. Art constraints

### What to keep
- muted palette
- hand-drawn contours
- slightly dirtied textures
- simple silhouettes
- relatively low detail density
- composition through landscape layering

### What to avoid
- too clean a fantasy look
- high fantasy ornamentation
- overly colorful UI
- generic 3D realism
- over-animation
- overly small micro-details that get lost in gameplay view

---

## 10. Success criteria

The prototype is successful if:

1. the style **holds together** during camera movement
2. camera rotation **does not break the illusion of the cutout world**
3. at first glance, the player can recognize:
   - the terrain
   - the river
   - the camp
   - the characters' roles
4. fog of war increases both readability and atmosphere
5. the UI is usable while not being distracting
6. the first scene feels like a **game with its own identity**, not just a generic mockup

---

## 11. Open questions to verify

1. How well does camera rotation work with billboarded trees?
2. What will larger buildings look like at different angles?
3. Do some objects need "fake depth" or a multi-layered cutout?
4. ~~Is a strictly orthographic camera better, or a slightly perspective one?~~ Settled: perspective (see section 2).
5. How strong should the fog of war stylization be?
6. How detailed should terrain shading be so it doesn't overpower the characters?
7. Should the UI lean parchment-like, or dark metal-and-wood?

---

## 12. Recommended order of work

### Sprint 1
- terrain sandbox
- camera
- a few trees / rocks / a river
- 1–2 characters
- rotation test

### Sprint 2
- camp
- billboard assets
- 4 character roles
- fog of war
- basic UI

### Sprint 3
- gameplay mockup
- small settlement
- winter variant
- first asset sheet

### Sprint 4
- polish
- comparison of variants
- decision on the final visual pipeline

---

## 13. Summary for the agent
The main task is not to "make a pretty picture," but to **verify that this specific style can be sustained in an interactive scene**.

The priority is:
1. **terrain + camera + occlusion**
2. **billboard / cutout objects**
3. **readable character roles**
4. **fog of war**
5. **simple stylized UI**

The output should feel like:
> a raw, illustrated, paper-cutout realtime strategy game set in a living, textured landscape.
