# Materials and Crafting Architecture — Design Exploration

## Status

**Step 1 of section 11 is implemented** (`ManyWinters.Core/Materials/`): `MaterialDefinition`
with `Density` and `Insulation`, `MaterialId`, `FormId`, `MaterialCatalog`, a `materials`
content folder, and `ItemDefinition` now stating a material, a form and a volume instead of an
authored weight and insulation — `ItemCatalog.WeightFor` multiplies the material's density by
the item's volume, and `InsulationFor` reads the material. Content densities and volumes were
chosen so every shipped item's derived weight equals the number it used to state, so the step
changed no gameplay.

Everything from step 2 onward is unimplemented. Two things are knowingly unfinished:

- **Nothing reads a form yet.** `FormId` exists so content already says what shape each item
  is; the predicates that ask arrive in step 2.
- **Only the two properties something reads are defined.** Hardness, toughness, flexibility,
  elasticity, fibrousness, flammability and plasticity are absent by the rule in section 2 and
  arrive with their readers.

What still exists, and this design still replaces:
`RecipeDefinition(Output, InputItem, InputAmount)` — one input kind, one output kind,
`axe = 5x wood` — plus `SkillDefinition.Tool` / `ToolHarvestBonus`, a whitelist saying
"this item kind is the tool for this skill". Both are authored per outcome, which is
what makes the item roster grow by hand. The placeholder recipes are also now visibly at odds
with the materials: the axe is stone and the warm clothing is hide, but both are still crafted
out of wood. Step 4 removes the recipes rather than reconciling them.

Also still existing, and folded into the same replacement: `BuildingDefinition(Id, DisplayName,
RequiredItem, RequiredAmount)` / `BuildingCatalog` / `BuildingKindId` and `ConstructCommand`.
Beside `RecipeDefinition` and `CraftCommand` they are the same twenty lines twice — check the
person is alive, remove the input, produce the output — differing only in where the output
lands, and that difference is stated by the *type* of the output id rather than by anything
about the object. Section 5 says why that is the wrong place to state it and what replaces it.

What already exists and this design builds on rather than replaces: `TechniqueId` /
`Person.KnownTechniques`, with teaching (`TeachCommand`, casual teaching in
`WorldState.Advance`) and loss on death; and the seeded-randomness convention
(`WorldState.CasualTeachingSeed`, `IdleTask.SeedFor`) — deterministic from ids and tick,
never a shared mutable `Random`.

Settled in discussion: no cap on assembly part count (section 6); discovery is both
autonomous and player-driven (section 7); function scoring lands before the first verbs
(section 11); a building is not a different kind of thing from an item, only a placed one, and
where a made object lands is derived from its weight rather than authored (section 5).

---

## 1. Material and form are two different things

Giving materials properties is not enough on its own. A substance and the shape it has
been worked into have to be separate axes:

- **Material** — the substance: grass, wood, stone, hide, sinew, clay, resin.
- **Form** — the worked shape and what it affords: fibre, cord, shaft, wedge, sheet,
  lump, powder, vessel.

An axe is *a wedge of hard material bound to a shaft of stiff material*. Grass rope is
*a cord of twistable fibrous material*. A stone lump cannot cut; a stone flake can —
same material, different form.

Without this split the design collapses back into authored outcomes wearing a new name:
`stone` would need a `canCut` flag, which is a recipe with extra steps.

---

## 2. Store physics, derive affordances

Whether a thing can be squeezed, crushed, twisted or bent is **not stored**. Those are
derived predicates over stored physical properties:

```
CanTwist      = Fibrousness > 0.5 && Flexibility > 0.4
CanKnap       = Hardness > 0.7 && Toughness < 0.3     // hard and brittle
CanCrush      = Toughness < 0.4
CanBend       = Flexibility > 0.5
HoldsTension  = Elasticity > 0.6                       // bow, snare, trap trigger
```

These are pure functions of a material definition, so they are testable without any
engine setup — the kind of extracted pure helper `docs/conventions.md` asks for.

First cut of the stored set, deliberately small:

| Property | Read by |
| --- | --- |
| `Hardness` | edges, wear, `CanKnap`, chopping/cutting scores |
| `Toughness` | `CanCrush`, `CanKnap` (as its inverse, brittleness), durability |
| `Density` | derived weight, impact force |
| `Flexibility` | `CanBend`, `CanTwist`, weaving |
| `Elasticity` | `HoldsTension` — distinct from flexibility: hide is pliable, not springy |
| `Fibrousness` | `CanTwist`, splitting, cordage |
| `Flammability` | ignition, fuel value |
| `Plasticity` | clay/wet hide/snow: holds a worked shape, then sets irreversibly |

Plus the food properties (`Nutrition`, `Toxicity`) and `Insulation`.

**Rule to hold the line:** a property is not introduced unless at least one affordance
predicate or one function score reads it. Without that rule the set inflates to thirty
numbers of which twenty-five are decoration.

### Existing fields that must become derived

`ItemDefinition.Insulation` and `ItemDefinition.Weight` are authored numbers today. In
this design they are consequences of material and form — `Weight` from `Density` and
volume, `Insulation` from the material. They have to *become derived*, not sit beside
the material properties as a second authored source of the same truth. (This is
`docs/conventions.md`, "look for the slot before adding the field": the slot exists, it
just has to change owner.)

---

## 3. Verbs

Three groups, because each has a different mechanic.

**Reductive — one material in:** `Twist`, `Split`, `Strip` (bark/bast), `Knap`, `Crush`,
`Grind`/`Sharpen`, `Pound` (soften fibre, or mill), `Bend`, `Dry`, `Soak`, `Char`,
`Fire` (plasticity to hardness, irreversible).

**Combinative — two objects in:** `Bind`/`Lash` (cord plus two objects to an assembly),
`Glue` (needs adhesion — resin, pitch), `Wedge` (haft into a socket), `Wrap`, `Fill`.

**Applicative — use, not craft:** `Strike A with B`, `Cut A with B`, `Pry`, `Throw`,
`Ignite`.

Every combinative verb is **binary**. This is what keeps assembly depth emergent instead
of capped (section 6).

Each verb is a `TechniqueId`, so it inherits teaching, sharing, and loss on death for
free.

---

## 4. Function scores, and why they come first

The same properties that decide craftability must decide effectiveness in use. An axe is
not "an axe"; it is a hafted hard wedge, and when a person fells a tree the simulation
asks *does this object have an edge, mass and a handle?*

```
ChoppingScore = EdgeSharpness * Hardness * sqrt(Mass) * HaftLeverage
```

Work commands stop asking "does the person carry `skillDefinition.Tool`?" and start
asking the inventory for the best-scoring object for a named function. A stone flake in
the hand then scores badly, a hafted flint axe scores well, and a bronze axe later slots
in with better numbers and no new code.

**Without this step, property crafting is a decorative front-end for the same fixed item
list.** It is also the first step that changes anything a player can see, which is why
it is scheduled before the first verbs.

---

## 5. Two-tier inventory

`Inventory` is `Dictionary<ItemKindId, int>` today, and composite objects do not stack.
Rather than making everything an instance:

- **Materials stay stackable** — `grass: 12`, exactly as now.
- **Only assemblies become instances**, carrying their part list and accumulated wear.

Roadmap step 8 already lists "item instances", so this is on-plan rather than a detour.
Save format churn is confined to the assembly tier.

### Buildings are the instance tier, already

`Building` — `Id`, `Kind`, `Position`, `Condition`, `Inventory` — is exactly the shape this
section wants for an assembly instance: one object, its own wear, its own contents. Only its
name and its `BuildingKindId` key say otherwise. Kept as it is, step 4 would found a second
instance class beside it, and wear, repair, storage and every later per-object feature would
be written twice. A parallel view hierarchy already cost buildings their collision shape and
fog fade once (`docs/status.md`, step 4); the same mistake in the model would cost more.

So the two collapse into one:

- **One definition.** A storage hut is an `ItemDefinition` — material wood, a shelter form,
  a volume large enough that its derived weight exceeds any carry capacity — and
  `storage_hut = 20x wood` is a `RecipeDefinition` like `axe = 5x wood`. `BuildingDefinition`,
  `BuildingCatalog` and `BuildingKindId` go.
- **One make command with one placement rule.** What a person makes goes into their
  inventory if `WorldState.MaxCarryWeightFor` allows it, and otherwise comes into existence
  in the world within reach of the maker. Position is an optional argument, and the reach
  check applies only when placing. Nothing carries an "is a building" flag: the hut lands in
  the world because it is heavy. A `MakeCommand` with `if (isBuilding)` inside would be the
  two commands in one file, and is the failure mode to watch for.
- **One instance class.** `Building` is renamed to the placed-object instance this section
  describes and keyed by `ItemKindId`. `RepairCommand`, `ConditionDecayPerTick` and
  `DepositCommand` / `WithdrawCommand` keep working on it unchanged; they stop being
  "building" features. Step 4 then extends this class with the part list instead of founding
  a new one.

What this deliberately does not model yet, and must not be stretched to cover: anchoring (a
pit, a shelter lashed to a standing tree) and the spectrum between carry, drag and immovable
(a canoe or a sledge is too heavy to carry and still moves). Both need parts and joints — the
ground or the tree is a part the assembly is bound to — and so arrive with step 4. For today's
one hut, weight alone is enough. The same instance tier is also where an unfinished object
lives: a half-built shelter is an assembly missing parts, which anyone can add to and only
someone who knows the technique can finish. Today's instantaneous `ConstructCommand` cannot
express that, and it is the construction-side reason to want the merge beyond saving code.

---

## 6. Object model: no part-count cap

An object is a recursive list of parts and joints; a part is (material, form, quality).
There is no `MaxParts` constant.

The reasoning: the recursive data model costs almost nothing, and an arbitrary cap would
be a declared limit where a consequential one is available for free. Because every
combinative verb is binary (section 3), depth arises naturally — bind a bound thing to a
third thing — without any number to pick and defend.

Absurd assemblies are handled by physics, not prohibition:

- mass accumulates, and carry weight already constrains it,
- every joint is a weak point: `Durability = min` over parts and joints,
- function scores fall off as junk parts are added.

So a rope lashed to a rope lashed to a rope is constructible and useless, which is a
better answer than "not allowed".

What genuinely does grow with depth is the *downstream* work — archetype recognition,
sprite selection, UI. Those are content and presentation concerns, and they can stay
shallow (recognise the patterns worth recognising, generate a name for the rest) while
the model underneath stays general.

---

## 7. Discovery

Three layers, and both autonomous and player-driven paths through them.

1. **Property knowledge.** Handling a material teaches its properties — gathering grass
   teaches "grass is fibrous and pliable". This is per-person knowledge, so material
   understanding is taught, inherited and forgotten exactly like a technique — not like a
   skill level, which is never transmitted at all (see "What transmission already does"
   below).
2. **Verb discovery.** Knowing "fibrous and pliable" opens a chance to discover `Twist`.
3. **Pattern recognition.** Holding cord, a wedge and a shaft opens a chance to discover
   `Bind`. The first successful bind names the pattern — "axe" arrives as a recognised
   configuration, not as an authored recipe.

### Autonomous path: idle experimentation

`IdleTask` becomes "fiddle with what I am carrying". Idle people already exist and idle
time is currently dead, so this costs little and turns it into something: a person idling
with grass rolls against discovering twisting, which gives the player a reason to leave
someone by the fire with materials.

Two modifiers worth having:

- **Need bias** — someone cold is likelier to discover insulation-related verbs, someone
  hungry food processing. The cheapest way to make discoveries feel narratively apt.
- **Negative knowledge** — attempting `Bind` with something non-fibrous teaches "this
  does not work". Prevents retry-spam and makes learning look like learning.

Discovery rolls follow the existing seeded pattern (deterministic from ids and tick) so
they can be asserted in tests.

### Player path: "try X on Y"

The player picks two things a person carries and orders an experiment. The simulation
adjudicates against real properties; the cost is time, and material on failure.

This is the loop worth playing — the player forms a hypothesis and the simulation rules
on it — and it lets a player outpace what the simulation would have discovered on its
own, which is where the skill expression lives. Both paths feed the same three layers;
neither is a shortcut around the other.

### How the two paths differ

Directed experimentation is faster than idle fiddling, but rate is the least interesting
difference. What the player is really buying is **aim**:

| | Idle experimentation | Directed experiment |
| --- | --- | --- |
| Target | undirected — whatever is carried, chosen at random | aimed — this material, this pairing |
| Rate | slow | fast |
| Reach | only materials whose properties the person already knows | may attempt a material the person knows nothing about, and learn the property by trying |
| Cost | free; the time was dead anyway | the person's time, and material on failure |
| Risk | destroys nothing | may waste the material |

The reach row matters as much as the rate row. **Idle consolidates; directed explores.**
If the two paths were the same roll with different multipliers, idle would be nothing but
a slow player and the distinction would collapse — the player's own understanding of the
material model is what should convert into progress.

Cost is what makes the choice real: idle is free because that time was already dead, so
directing an experiment means pulling someone off useful work.

**Idle discovery must stay non-zero**, not merely very slow. It is what keeps knowledge
living in people rather than in the player's head: a settlement left alone still develops,
and a new group after an extinction re-derives things for itself. If idle discovery were
decorative, effectively only the player would ever discover anything.

### Calibrating the idle rate

Express the idle rate as *how many winters a settlement left entirely alone takes to reach
cord*, not as a per-tick probability. That is the figure a designer can reason about, and
`ManyWinters.Tools.SimulationRunner` (roadmap step 3) can measure it headlessly.

Optionally, per-person variation — some people are more curious than others — would make
idle discovery a matter of personality and give characters distinction. Noted as an option,
not a decision.

### What transmission already does, and what stays out of scope

Person-to-person transmission is not this plan's problem — most of it already works — but
three separate things get confused under that heading, so the boundary is worth stating.

**Information already transmits.** `TeachCommand` (player-directed) and
`WorldState.AutoTeachNearbyPeople` (autonomous, a roll per tick, critical techniques at a
higher chance) already pass techniques between people, and graves preserve
`KnownTechniques`. Because property knowledge and verbs are techniques (section 3), they
ride this channel for free — no new transmission code.

**Experience deliberately does not transmit, and must not start.** Teaching only adds to
`student.KnownTechniques`; `Skills` levels are raised solely by a person's own actions, and
`AutoTeachNearbyPeople` explicitly excludes every skill's `EfficientTechnique` from casual
teaching. A student learns *that a thing can be done* and then has to build competence
themselves. Verb execution quality must follow the same rule — being taught `Knap` must not
confer skill at knapping.

**Distorted transmission — knowledge degrading as it passes along a chain — exists nowhere
and is not in this plan.** It has its own document,
`docs/knowledge-transmission-architecture.md`, because it is a change to how knowledge is
*represented*, it would apply to every technique (foraging, burial, eating), and this plan is
already retiring recipes, the tool whitelist and the item model. That plan lists steps 1-5
here as a hard prerequisite.

### The one decision here that gates it

Property knowledge is the first knowledge in the game that can be **wrong rather than merely
absent**. `basic_foraging` is known or unknown; but "cord needs something stiff" is a
mistaken belief that makes a person genuinely reach for the wrong material and fail. That is
the substrate distorted transmission needs in order to have anything to corrupt — so while
this plan must not build it, this plan is what makes it worth building.

Which means the representation chosen here decides whether it stays cheap later:

- as `TechniqueId("grass_is_fibrous")` it is binary forever, and there is nothing to distort;
- as a **belief** — `(MaterialId, PropertyId, believedValue, confidence)` — distortion is a
  later addition needing no rework, because a corrupted belief is just a wrong
  `believedValue`.

The same applies to the negative knowledge above: "this does not work" is also a belief that
can be mistaken — someone was told grass will not twist, and so never tried.

Recommendation: model property knowledge as a belief from the start, even while every belief
is transmitted perfectly and no distortion exists. Verbs themselves can stay plain
`TechniqueId`s.

---

## 8. Naming and archetype recognition

When an object is a bag of properties, the UI cannot call it "axe" unaided. A small
catalogue of patterns handles it: *hard wedge above a mass threshold, hafted to a stiff
shaft, reads as "axe"*. Unrecognised configurations get a name generated from the
material and form vocabulary — "lashed stone club", "twisted grass cord".

This layer is cosmetic for naming but load-bearing for art: **it is also what selects the
sprite**, so it meets `docs/sprite-pipeline-architecture.md` and should be designed with
it in view rather than after it.

Naming is also a narrative beat available cheaply: the settlement names the thing it just
invented, and the name outlives the inventor.

---

## 9. Risks

- **Combinatorial junk.** N materials by M verbs is mostly nonsense. Mitigated by strict
  property gates (most combinations are simply refused) and by the player only ever
  seeing what someone knows.
- **Property inflation.** Held off by the rule in section 2.
- **Optimisation collapse.** Once flint is known to be the best edge, choice dies.
  Mitigated by gating material *availability* through climate and geography rather than
  gating recipes, and by real trade-offs — denser is more effective but heavier, and
  carry weight is already a live constraint.
- **Legibility.** Never show numbers. Hover says "hard, brittle, heavy", not
  `0.82 / 0.19 / 0.71`.

---

## 10. What this removes

- `RecipeDefinition` / `RecipeCatalog` / the current `CraftCommand` shape.
- `BuildingDefinition` / `BuildingCatalog` / `BuildingKindId` / `ConstructCommand`, folded
  into item definitions, recipes and the one make command with its placement rule
  (section 5). `Building` itself is renamed and rekeyed, not removed.
- `SkillDefinition.Tool` and `ToolHarvestBonus`, replaced by function scoring (section 4).
- Authored `ItemDefinition.Insulation` / `Weight` values, replaced by derived ones
  (section 2).
- An open item in `docs/todo/todo.md`: *"craft axe must not be shown when the character
  has not gathered the material and has not invented the axe yet."* In this model it
  resolves itself — an action nobody knows does not exist in the menu.

Per `docs/conventions.md`, each of these is deleted at the point it becomes dead, not
left behind a comment.

---

## 11. Implementation order

1. `MaterialDefinition` with properties, and `FormId`. `Insulation` and `Weight` become
   derived. No new gameplay — foundation only.
2. Derived affordance predicates as pure functions (`CanTwist`, `CanKnap`, ...). Fully
   unit-testable, no engine involvement.
3. **Function scores, and rewriting tool use.** `Fell` / `Gather` stop consulting
   `SkillDefinition.Tool` and start asking for the best-scoring object. First visible
   change, and the most valuable single step.
4. The assembly instance tier from section 5, beginning by folding buildings into it: one
   definition, one make command with the weight-derived placement rule, `Building` renamed
   and rekeyed by `ItemKindId`. Then the first two verbs end to end: `Twist` (reductive) and
   `Bind` (combinative). The building merge depends on nothing in steps 2 and 3 and can be
   done ahead of them; it is under step 4 because it is the first move of the instance tier,
   not because it waits for the verbs.
5. Discovery: property knowledge from handling, verb discovery from idle experimentation,
   pattern recognition on first successful assembly.
6. Player-directed "try X on Y", and properties surfaced as words in the UI.
7. Remaining verbs as content, not code.

Steps 1-3 can be done without touching discovery at all, and on their own they retire the
tool whitelist.

---

## 12. Open questions

- Where do form transitions live — is `Form` an enum with verb-authored transitions
  (`fibre --Twist--> cord`), or does each verb declare its own output form? The second
  scales better with content but scatters the vocabulary.
- Does quality belong on the part (each part separately worked) or on the assembly (one
  number for the finished thing)? Per-part is more faithful and multiplies bookkeeping.
- How does a function score expose itself to the player without becoming a number
  (section 9's legibility rule) — comparative wording ("chops better than the flake"), or
  nothing at all, letting outcomes teach it?
- Do resource nodes carry material identity directly, so that felling a particular tree
  yields *that* wood with its own properties, or is material resolved per resource kind?
  The former opens per-region material variation; the latter is what the current
  `ResourceDefinition.YieldsItem` shape assumes.
