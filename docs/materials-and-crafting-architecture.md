# Materials and Crafting Architecture — Design Exploration

## Status

**Steps 1-6 of section 11 are implemented and step 7 has begun** (one verb of it),
though the labels below do not say so.

### A note on the numbering

Everything from the first verb onwards is labelled `4c-N`, and after `4c-3` that stopped
being true. Step 4c-4 was already step 5's work, and each later piece took the next `4c`
number by habit rather than by belonging there. The entries are left as they were written -
relabelling finished history reads worse than marking it - so this is where they line up
against the plan:

| Section 11 | Written up as |
|---|---|
| 5 — property knowledge from handling | 4c-6 (beliefs), and 4c-7/4c-8 for how it travels and decays |
| 5 — verb discovery from idle experimentation | 4c-4 |
| 5 — pattern recognition on first successful assembly | 4c-9 (`AssemblyPattern`, `Vocabulary`) |
| 6 — player-directed "try X on Y" | 4c-2 (the workbench), 4c-3 (the attempt and its dice) |
| 6 — properties surfaced as words | 4c-5 (`MaterialWords`) |
| 7 — remaining verbs | 4c-10 (`Knap`), the first of them |

Steps 1-3 keep their own numbers below and mean what they say.

Step 1: `MaterialDefinition` with `Density` and `Insulation`, `MaterialId`, `FormId`,
`MaterialCatalog`, a `materials` content folder, and `ItemDefinition` now stating a
material, a form and a volume instead of an authored weight and insulation —
`ItemCatalog.WeightFor` multiplies the material's density by the item's volume, and
`InsulationFor` reads the material. Content densities and volumes were chosen so
every shipped item's derived weight equals the number it used to state, so the step
changed no gameplay.

Step 2 (2026-09-19): `MaterialDefinition` gained `Hardness`, `Toughness`,
`Flexibility`, `Elasticity`, `Fibrousness` (all `0f` by default; no content authored
yet, since nothing reads them for gameplay until step 3), and `MaterialAffordances`
holds the five pure predicates from section 2 (`CanTwist`, `CanKnap`, `CanCrush`,
`CanBend`, `HoldsTension`), unit-tested at their boundaries with no engine
involvement. This step also changed no gameplay.

**Step 3 (2026-09-19, in two passes):** first pass replaced `SkillDefinition.Tool` /
`ToolHarvestBonus` with `SkillDefinition.UsesChoppingScore` (bool) and
`ItemCatalog.ChoppingScoreFor` / `Inventory.BestChoppingScore` - `GatherCommand` and
`FellCommand` now ask the inventory for its best-scoring object instead of checking for
one authored tool item kind. That pass left the score at `Hardness * sqrt(Weight)`, so
a raw stone lump scored exactly as well as the stone axe; landing the *mechanism*
before the *formula* was deliberate and agreed, since nothing else depended on the
number being right yet.

Second pass closed that gap: forms are now content in their own right
(`FormDefinition` / `FormCatalog`, a `forms/` folder beside `materials/`), the score is
`EdgeSharpness * Hardness * sqrt(Weight)`, and only a `wedge` carries an edge. A lump
of the very same stone the axe head is made of therefore scores nothing, which is
section 1's split between geometry and substance finally doing visible work. Authored
so far: `Hardness` on `stone`, `EdgeSharpness` on `wedge`; everything else sits at
zero, so nothing but woodcutting is affected.

Section 4's full formula, `HaftLeverage` term and all, arrived in step 4c-10, once
there were objects made of parts to have a haft at all.

Step 4a (2026-09-19): the `BuildingDefinition`/`ConstructCommand`+`CraftCommand`
duplication section 5 describes is folded - see section 5's "Step 4a done" note.

Step 4b (2026-09-19): `Assembly` (`ManyWinters.Core/Materials/`) is the recursive
object model of section 6 - a closed two-case union of `Part` (material, quality,
volume) and `Joined` (joint strength, two sub-assemblies), with `Weight` (density
times volume, summed) and `Durability` (the weakest of joint, left and right).
Uncapped depth, no `MaxParts`. Foundation only: nothing produces or stores an
assembly yet, so this changed no gameplay, exactly as steps 1 and 2 did.

Step 4c-1 (2026-09-19): the first verb, `Twist`, end to end - and with it the two-tier
`Inventory` of section 5, which only a verb could justify. `Inventory` now holds
stackable counts *and* a list of worked objects; `TotalWeight` adds both, so carry
capacity is honest about a pack full of cord. `TwistCommand` reads what an item turns
into from the item itself (`FormTransition` on `ItemDefinition`, the settled "where a
verb lands is the item's own business" of section 3) and whether the substance will
take a twist at all from `MaterialAffordances.CanTwist`, so neither answer is authored
per outcome. Bulk carries over from what went in, so twisting conserves weight. The
piece's `Quality` is its maker's practice at the moment of making, floored so a
beginner's first cord is poor but not worthless. The worked thing is named from its
own material and form ("plant fibre cord"), which is section 8's fallback naming
standing in until there are patterns worth recognising. It is offered on the person's
own card, and directing it teaches it, exactly as pointing at a tree teaches gathering.

Step 4c-2 (2026-09-20): `Bind`, and the workshop panel that `Bind` forced - a card of
flat buttons cannot express "this one with that one" without a line per pair, which
would also hand the player the list of pairs that work.

`BindCommand` takes two `BindTarget`s, either of which may be a unit of raw stock or a
worked thing already carried, which is what makes depth need no special case: a bound
thing is a thing, so it binds again. The cordage is not asked of the player - the
simulation reaches for the soundest binding in the pack - and it is consumed, its
weight passing into the joint (`Assembly.Joined.JointWeight`), so nothing is created or
destroyed by tying a knot. What may serve as cordage is content's call, not the
command's: `FormDefinition.LashingStrength` gates it and feeds the joint's strength
along with the cord's own durability and the binder's practice.

The panel (`WorkshopPanel` + the testable `WorkshopActions` beside it) opens from the
pack line on the person's card, holds the clock exactly as the pause page does, and
offers one button: "Try it". The number of things picked is the whole question - one is
a reductive verb, two a combinative one - so the panel's shape does not grow as the verb
vocabulary does, and nothing anywhere lists the verbs. A pick that leads nowhere comes
back as no offer at all and the bench says only "Nothing comes of it."; a pick that is
refused for a reason carries the world's own answer, as every other offer does. The
card's per-verb "Twist grass" line from 4c-1 is gone, replaced by this.

The save format changed with it: a bound object is recursive, so `AssemblySaveData`
mirrors `Assembly`'s two cases as two nullable blocks. The flat shape 4c-1 shipped would
have dropped a bound object silently on save.

Step 4c-3 (2026-09-20): the first half of the discovery layer - a directed attempt can
now fail. `WorkAttempt` rolls it, deterministic from the person's seed, the verb and
the tick, as every other roll in the game is. Skill moves the odds without opening or
closing the door: a beginner lands about one try in five and a practised hand never
fails, which is section 7's "success modifier, not a gate". The same curve gives the
quality of what comes out, because it is one fact about a person seen twice.

**An attempt costs time** (`SimulationRules.TicksPerWorkAttempt`), and that is load
bearing rather than flavour: the bench holds the clock, so without a time cost a player
could press "Try it" at a frozen tick until the same roll finally came up different -
except it never would, the roll being a function of the tick. Paying time is what makes
a second try a second roll. Failure also costs material, and differently per verb: a
spoiled twist wastes the handful, while a slipped lashing wastes only the cordage -
two things that came apart are still two things. Both teach, so a wasted attempt is
still practice.

Step 4c-4 (2026-09-20): idle experimentation - a person with nothing else to do and
something in their hands now works verbs out for themselves. The rule is one sentence:
**you work out how to do the thing you could have done already, if only you had known
how.** Each verb's own command is asked what stands in the way, and discovery happens
exactly when the answer is `NotLearned` - so nothing in the discovery pass re-states
what a verb needs, and a verb that grows a new requirement is obeyed there for free.
What gets tried is undirected, as section 7 says it must be: one thing out of the pack
or two, taken at random from the same seeded stream as every other autonomous roll.
Aim is what the player buys by directing an attempt, and what makes the time it costs
worth paying.

The rate is `SimulationRules.IdleDiscoveryChancePerTick` multiplied by
**`Person.Curiosity`**, which sits on the person rather than in the rules on purpose:
an NPC tribe that should develop more slowly than the player's band is the same world
with a lower number, not a second set of rules (`docs/todo/todo.md`, NPC tribes).
Uniform within a band today; section 7's per-person variation is still only an option.

**The player's own band is deliberately slow at it too**
(`SimulationRules.StartingBandCuriosity`, a quarter of the base rate). A band that
worked things out briskly by itself would leave the player watching rather than
playing, and teaching them is the game - so what they manage alone is a slow floor
under a player who has missed something, not a substitute for leading them. A child
takes its mother's rate rather than the player band's, so a tribe's children stay on
the tribe's.
Calibrated as section 7 asks - non-zero but measured in winters, and the test asserts
it across a crowd rather than one person, since one person's roll is a function of
their own seed.

Step 4c-5 (2026-09-20): properties surfaced as words (`MaterialWords`), which the
workbench shows for a single pick - "It is fibrous, pliable, light." The bench was
otherwise blind guessing: section 7 asks the player to form a hypothesis, and a
hypothesis needs something to go on, while section 9 forbids showing the numbers. The
words say what a thing *is*, never what it is *for* - no line hands the player a verb,
because working that out is the game. Two things picked at once are described by
nothing: that is a question about the pair, and a wall of adjectives is not an answer.
Capped at three words, since a thing described four ways at once has said nothing.

It lives in Core rather than beside the rest of the player's prose, because the
chronicle will want to describe a thing in the same words the panel does - the point
section 4 makes about function-score wording.

Shipped materials were given real property values at the same time (`wood`, `stone`,
`hide`), which they had gone without since step 2 for want of a reader. That also
settled a wart the words exposed: `stone` had `Toughness` at zero, which
`MaterialAffordances.CanKnap` read as perfectly brittle while the words read it as
"nobody said". A property that two readers disagree about is a property that wants
authoring.

Step 4c-6 (2026-09-20): **beliefs**, the decision section 7 gates everything else on.
`Beliefs` holds, per person, what they take each property of each substance to be, and
how sure they are. Nothing writes a wrong belief yet - everyone who learns one learns
it true - but every *reader* already copes with one, so distortion is a new writer
rather than a rewrite.

The shape that made this cheap: `Beliefs.AsBelieved(actual)` returns a
`MaterialDefinition` as that person understands it, so `MaterialAffordances`,
`MaterialWords` and anything else that reads a material can be pointed at somebody's
understanding without knowing beliefs exist. No new intermediate type, and no reader
had to change.

Beliefs come from **handling**: carrying a thing about teaches what it is like, over
roughly a season (`SimulationRules.MaterialUnderstandingPerTick`), including every
substance inside a made object. Below firmness a person has an inkling rather than
knowledge, and `AsBelieved` leaves the property blank - *not* the truth leaking
through. So a band that never picks anything up learns nothing about anything.

This is also where section 7's **reach** difference finally bites: idle hands only turn
over the familiar, so nobody idly works a substance they have not come to know, while
the player may direct an attempt on anything. Aim, not just speed, is what directing
buys. The workbench likewise describes a substance as the *selected person* believes it
to be, so a material nobody has handled says nothing.

Step 4c-7 (2026-09-20): beliefs now spread two more ways, which between them close
section 7's "How the two paths differ" table.

**Telling.** People standing together mention what substances are like
(`WorldState.ShareWhatTheyKnow`). Talk rather than instruction: it does *not* go through
`TeachCommand` and needs no teaching skill, so understanding spreads through a band
before anyone has learned to teach. Section 7 assumed property knowledge would ride the
technique channel "for free"; it does not, since that channel carries `TechniqueId`s.
What is passed is only what the teller would act on themselves, and it lands as
**hearsay** (`SimulationRules.HearsayConfidence`) - held less firmly than what the
listener could have found out by handling it. One mention is talk, two are conviction,
and handling the stuff settles it. That is the seam distortion will run along.

**Trying.** A directed attempt teaches the properties of everything it had hands on -
both things joined and the cordage - at once and firmly, whether or not it came off
(`WorkAttempt.TeachesWhatItIs`). This is the *reach* the player buys: somebody can be
sent to try a substance the whole band understands nothing about and come back
understanding it, where idle hands only ever turn over the familiar.

It also makes **negative knowledge** fall out rather than needing a notion of its own:
section 7 wanted "attempting Bind with something non-fibrous teaches this does not
work", and now a spoiled attempt teaches the very properties that explain why - after
which neither the person nor the idle pass reaches for that stuff again. A mistaken
account can equally put somebody off trying, because the same belief can be wrong.

Step 4c-8 (2026-09-20): **distortion**, which is what the belief shape existed for.
A retelling now strays from what the teller believes
(`SimulationRules.HearsayDistortion`), narrowed to nothing by the teller's skill at
teaching. Error is laid on what the teller already held, so it accumulates along a chain
without any hop count, and two people who heard a thing from different directions come to
disagree. Details and what remains are in `docs/knowledge-transmission-architecture.md`,
whose prerequisite - beliefs rather than bare techniques - this plan has now met.

That doc's sequencing rule was followed: **correction before corruption**. Both
correcting forces (handling, and working a thing) were already in place from 4c-6 and
4c-7, so the world was never in a state where knowledge only decayed. Retelling also had
to be loosened to make chains possible at all: people repeat what they were *told*, not
only what they have proved, or a tale could never travel past its first listener - though
a vague inkling is still beneath saying out loud.

Step 4c-9 (2026-09-20): **the band names what it makes.** A made thing had been described
by its substance and shape ("lashed stone wedge and wood stick"), which is a description
and not a name. Section 8 wanted recognition against a catalogue of authored patterns; that
was dropped in discussion for something better - the player types the word, at the moment
of discovery, and the band keeps it.

`AssemblyPattern.SignatureOf` is what the word is hung on: forms only, children sorted, so
the word is for the *shape*. An axe is an axe whether its head is stone or flint, which is
how a band gets the word for free on everything it makes that way afterwards - including
out of substances nobody had then heard of. Depth tells things apart, so a thing with
another thing lashed to it is not the first thing.

`Vocabulary` holds the words, on the world rather than on a person: it is the band's
language, not one settler's. `InspectorText.ForWorkedThing` asks it first and falls back to
the description, so a named thing is called by its name everywhere at once. Naming is
offered in the workshop when something comes out that the band has no word for, and it
writes a chronicle line rather than taking over the screen - the player has just typed the
word, and telling them what they did on a full page is ceremony in the way of playing.

Step 4c-10 - **section 11's step 7, the first of the remaining verbs** (2026-09-20):
**knapping, and the first real axe.** The arc that started at "a
tree cannot be felled without an axe" closes here: `KnapCommand` strikes a stone lump into a
wedge - the first edge in the game - and `ItemCatalog.ChoppingScoreOf` finally scores a made
object, `HaftLeverage` included. A band now fells trees with a thing it worked out how to
make, and **the `axe = 5x wood` recipe and the `axe` item are gone** along with the button
that made one.

Nothing in the game is called an axe. What fells a tree is a chopping score, and a hafted
knapped wedge has one: the head's edge, its substance's hardness and its mass, multiplied by
what the haft lends it - narrowed by the joint, because a head that wobbles is a head swung
by hand however long the shaft. Which part is the head and which the haft is declared
nowhere; the object is scored both ways round and is whichever reading serves it better.
Per-part quality multiplies the edge, so a beginner's wedge is a poor one and practice shows
in use rather than only on a card.

Adding the second reductive verb is also what said what a reductive verb *is*:
`ReductiveWork` holds everything they do alike, so a verb is now three facts in one place -
what it is called, which skill it is, and what a substance must be like to take it (see
`MaterialAffordances`) - with the fourth, what each item it touches turns into, on the items
themselves. `TwistCommand` and `KnapCommand` keep their own names because the
world speaks in them, but there is one copy of the machinery. `ReductiveVerbs.For` answers
"which verb does this one thing answer to" from the item's own transitions, and both the
directed path (`WorkshopActions`) and the autonomous one (`WorldState.TrialOf`) ask it - so
the workbench grows no wider as the vocabulary does, and nothing anywhere lists the verbs.

What is knowingly left conserved: knapping neither wastes stone nor keeps the flakes, because
`FormTransition` states no yield and adding one would be a second number to author and keep
in step (see step 4c-1). A wedge therefore weighs its lump. It arrives if and when something
reads it.

This step also tested step 7's promise that the remaining verbs would be *content, not code*,
and found it false. `Knap` cost a command record, a value in `ActionBlocker` and a line in
`ReductiveVerbs` on top of its content. Three of a verb's four facts are data - its name, its
skill, and what each item it touches turns into - but the fourth is the predicate saying what
a substance must be like to take it, and that is physics rather than authoring, so it stays
code on purpose (see section 2). Section 11 now says so.

Step 7, second verb (2026-09-20): **`Sharpen`**, and with it the first verb whose *subject* is
something already made. A wedge comes out a wedge - keener, and smaller, because an edge is
renewed by taking material off it. Quality up and mass down are both read by the one score
(`ItemCatalog.ChoppingScoreOf`), so grinding away at an already-good edge quietly makes a worse
tool and no rule had to say so.

What it is worth is bounded by the hand doing it: an edge comes up to what that person could
have struck themselves and no further. A practised knapper rescues the poor wedge somebody else
made, a beginner cannot improve on a master's, and a tool made in a first winter is worth
going back to once the hands have learned something. That is the whole reason the verb has a
reader today, since nothing wears out yet.

It is deliberately **not** a `ReductiveWork` verb: nothing changes form, so there is no
`FormTransition` to read, and it works on an `Assembly` rather than an `ItemKindId`. The edge it
renews may sit anywhere inside the object - it is found by shape and substance (which piece
presents an edge of something hard), not by mass or workmanship, because those decide how well
the thing chops rather than which part of it is the blade. One attempt renews one edge, even
where two pieces of the thing are alike in every particular.

The workbench and the idle pass both take a made thing picked alone now, where before only raw
stock could be worked. That is one corner of "a worked thing has nowhere to go" turned back.

### Why only one verb, when step 7 says "remaining verbs"

Section 3 lists 22 verbs. Four are done (`Twist`, `Knap`, `Bind`, `Sharpen`). The other 18 were
audited before this step and **not one of them can be added honestly today**, which is worth
recording so it is not re-derived:

- `Strip`, `Char`, `Fire`, `Dry`, `Soak` change *what a thing is made of*. `FormTransition`
  conserves material on purpose, so every one of them needs machinery that does not exist.
- `Split` turns one thing into two. A reductive verb yields one.
- `Crush`, `Bend`, `Pound` fit the machinery exactly and have **no reader**: crushed fruit
  cannot be eaten (`EatCommand` speaks in counts), a bent stave has nothing to be, softened
  fibre is read by nobody. Adding them would put three verbs in the game that do nothing, which
  is the rule this whole design works under (section 2: a property arrives with its reader).
- `Glue`, `Wrap`, `Fill` need materials or forms nobody has (resin; a vessel that is an assembly
  rather than an item).
- `Wedge` (haft into a socket) is `Bind` without the cordage, and would want a socket form to
  mean anything different.
- The five applicative verbs are a group of their own, not "remaining verbs": nothing in the game
  applies an object to a target today - `Fell` and `Gather` ask for a score instead - so they are
  a step, not an addition.

**What unlocks the next batch is not verbs.** Two things do: a way for a verb to change a
material rather than only a form, and somewhere for a worked thing to go (eaten, put down,
stored, inherited). `Crush` is the cheapest of them and wants nutrition as a property of the
material - the same move step 1 made with insulation and weight.

Beyond section 11 (2026-09-21): **what somebody made outlives them.** The plan runs out at step
7, so this comes from the list of knowingly unfinished things below rather than from a numbered
step. `LootCommand` now takes worked things off a body as well as counted stock, and
`TargetActions` offers "Take what they carried" for a body carrying only a made thing - which it
did not, so somebody who died holding nothing but the axe they had made showed no way to take it
at all.

What they made comes off the body **before** the firewood: a worked thing is the one thing the
band cannot simply go and gather again, and a pack filled with a corpse's stock would leave no
room for it. It is taken whole or not at all (`Inventory.AddAssemblyIfItFits`), because half an
axe is nothing - which is the difference between the two tiers showing up in one more place.

Burying changes none of this: a buried body stays in the world and stays lootable, so a grave is
not a way to lose a tool.

Still open in 4c: per-assembly
identity (section 6 - deferred a third time, and now for a stated reason: an assembly
is a value, so two that match in every part and joint are indistinguishable to anyone
who could tell them apart, and identity only starts earning its keep the day a worked
thing carries a maker or its own wear). Three things are knowingly unfinished:

- **A worked thing still cannot be put down or stored.** Inheriting one is done (see above);
  dropping, depositing and withdrawing all still speak in counts, so a made thing moves between
  a pack and a body and nowhere else. The remaining three share one design question that looting
  did not have to answer: a pile on the ground is one kind and one count today, so a made thing
  put down has no shape to be.
- **A joint names neither its verb nor its binder yet.** Section 6 describes both; step 4b
  left them out because nothing reads them until the verbs that set them exist (step 4c),
  the same rule that holds back unread material properties.
- **`Flammability` and `Plasticity` are still absent**, per the rule in section 2, because no
  predicate defined there reads them yet; they arrive with their readers.

What still exists, and this design still replaces:
`RecipeDefinition(Output, InputItem, InputAmount)` — one input kind, one output kind — authored
per outcome, which is what makes the item roster grow by hand. The axe went in step 4c-10, and
with it the worst of the mismatch the placeholders showed: what is left (bag, basket, warm
clothing, storage hut) is still visibly at odds with its materials, the warm clothing being hide
but made out of wood. Each goes the way the axe went, when the verbs that would make it exist.

Already replaced, and no longer to be planned for: `SkillDefinition.Tool` /
`ToolHarvestBonus` (the whitelist saying "this item kind is the tool for this skill") went in
step 3, and `BuildingDefinition` / `BuildingCatalog` / `ConstructCommand` / `CraftCommand`
went in step 4a — the last two were the same twenty lines twice, differing only in where the
output landed, which is now derived from weight by the one `MakeCommand` (section 5).

What already exists and this design builds on rather than replaces: `TechniqueId` /
`Person.KnownTechniques`, with teaching (`TeachCommand`, casual teaching in
`WorldState.Advance`) and loss on death; and the seeded-randomness convention
(`WorldState.CasualTeachingSeed`, `IdleTask.SeedFor`) — deterministic from ids and tick,
never a shared mutable `Random`.

Settled in discussion: no cap on assembly part count (section 6); discovery is both
autonomous and player-driven (section 7); function scoring lands before the first verbs
(section 11); a building is not a different kind of thing from an item, only a placed one, and
where a made object lands is derived from its weight rather than authored (section 5); a verb
is a fixed enum, but where it lands each item declares for itself rather than reading a global
form-transition table (section 3); quality lives on the part while identity lives on the
assembly, which is also where the deferred item-provenance idea attaches (section 6); function
scores surface as generated wording shared with the chronicle, not as numbers (section 4);
property knowledge is a belief `(MaterialId, PropertyId, believedValue, confidence)` from the
start, not a plain `TechniqueId`, so distorted transmission has something to corrupt later
without a rework (section 7).

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

### Where a verb lands is the item's own business

A verb is the same fixed enum for every item it can apply to - `Twist` never gets a
per-material variant. What it produces is not read from a global table keyed by form
(there is no single `fibre --Twist--> cord` rule everyone shares); each item states
its own outcome for the verbs that apply to it, the same way it already states its
own material, form and volume. Grass twisted is a grass cord; sinew twisted is a
sinew cord - two different outcomes for the same verb, declared where the rest of
grass's and sinew's own data already lives, not in a second table that has to be kept
in step with the item roster.

This keeps the verb vocabulary itself small and closed (a handful of enum values,
easy to teach and to draw icons for) while leaving content free to say precisely what
each material becomes, without either inventing a parallel form-state-machine or
falling back to an authored `RecipeDefinition`-style per-outcome table: the predicate
that gates *whether* the verb is even offered still comes from properties (section
2), only the *result* is authored per item.

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

### Surfacing a score: words, not numbers, and words the chronicle can reuse

A function score never appears to the player as a number (section 9). It surfaces as
comparative wording generated from the same properties the score reads - "chops
better than the bare flake", "barely holds an edge" - rather than as nothing at all:
silence would waste the one place the player learns that the simulation is actually
modelling the object, not just flavour-texting a fixed "axe" noun.

That wording is not a UI-only concern: `docs/chronicles-and-memory-architecture.md`
needs sentences about what a person made and used, and a function-score-derived
phrase is exactly the kind of fact a chronicle line already wants ("felled with a
poorly-hafted axe" reads like an epitaph detail, not a stat readout). The wording
function should live where both callers can reach it, not be written twice.

---

## 5. Two-tier inventory

`Inventory` is `Dictionary<ItemKindId, int>` today, and composite objects do not stack.
Rather than making everything an instance:

- **Materials stay stackable** — `grass: 12`, exactly as now.
- **Only assemblies become instances**, carrying their part list and accumulated wear.

Roadmap step 8 already lists "item instances", so this is on-plan rather than a detour.
Save format churn is confined to the assembly tier.

### Buildings are the instance tier, already - one third of this done independently

**Update 2026-09-19: partially overtaken by events.** A separate refactor
(`Refactor entities: unify Building, ResourceNode, and ItemPile as Entity`,
2026-09-17, unrelated to this design) already did the *instance class* merge this
section asks for, just not by the exact route described below - the original text
is kept struck through in spirit but corrected here so step 4 does not redo it or
contradict what already shipped.

What already exists: `ResourceNode`, `ItemPile` and `Building` are one `Entity`
class (`Id`, `Kind: EntityKindId`, `Category: EntityCategory`, `Position`) with
optional components - `Growth`, `StaticAmount`, `Condition`, `Storage` - and
`EntityCategory.Building` says which one a given entity is, so nothing is guessed
from which components happen to be populated. Keyed by `EntityKindId`, not
`ItemKindId` as this section originally proposed - the right call in hindsight,
since a placed thing's kind was never really an item kind to begin with, and this
section's own reasoning (one instance class, no parallel hierarchy) is exactly why
that refactor happened. `RepairCommand`, `DepositCommand` / `WithdrawCommand` already
run on `Entity` unchanged, as predicted.

**Step 4a done (2026-09-19) - both bullets below are resolved, kept for the record:**

- ~~Two definitions.~~ `storage_hut` is now an ordinary `ItemDefinition` (material
  wood, form `shelter`, volume 200 - deliberately far past any real carry capacity)
  and a `RecipeDefinition` like any other craftable. `BuildingDefinition`,
  `BuildingCatalog` and the `buildings` catalog folder are gone. `RepairCommand`
  reads its cost from `RecipeCatalog` instead. The building's own visuals
  (`Content/buildings/{kind}/{kind}.png`/`.tres`, read by `BuildingView`/
  `TexturePaths` off `EntityKindId` directly) were untouched - they were never wired
  through `BuildingCatalog` to begin with, only the now-deleted `.json` data file
  living in the same folder was.
- ~~Two make commands.~~ `ConstructCommand` and `CraftCommand` are gone, replaced by
  one `MakeCommand(Person, ItemKindId Output, Position? Position = null)`: it asks
  `Inventory.HasRoomFor` (the same live per-person check `AddUpToCapacity` already
  used elsewhere) to decide whether the output goes into the pack or is placed in
  the world within reach. `EntityCategory.Building` is still hardcoded for the
  placed case, since only one recipe needs it today - the first
  placeable-but-not-a-building output should turn that into a real per-recipe
  choice rather than stretching the hardcode.

What this deliberately does not model yet, and must not be stretched to cover: anchoring (a
pit, a shelter lashed to a standing tree) and the spectrum between carry, drag and immovable
(a canoe or a sledge is too heavy to carry and still moves). Both need parts and joints — the
ground or the tree is a part the assembly is bound to — and so arrive with step 4's object
model (section 6). For today's one hut, weight alone is enough. The same instance tier is
also where an unfinished object lives: a half-built shelter is an assembly missing parts,
which anyone can add to and only someone who knows the technique can finish. Today's
instantaneous `ConstructCommand` cannot express that, and it is the construction-side reason
to want the definition/command merge above beyond saving code.

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

### Quality is per-part; identity is per-assembly

Quality lives on the part, as the object model already states above — replacing one
part reworks only that part's quality, not a single number for the whole object. This
settles former open question "does quality belong on the part or the assembly?" in
favour of per-part: it is what makes "I replaced the handle on father's axe" a
sentence the model can actually represent — the haft becomes a new part with its own
quality while the rest of the assembly, and *what the assembly is*, does not reset.

That sentence needs one more thing the part list alone does not give: the assembly
itself has to persist as the same object across a part swap, not be re-derived fresh
each time from its current parts. An axe that has had its handle replaced twice is
still "father's axe", not a new axe that happens to look the same — the assembly
instance (section 5) carries an identity (at minimum: who first made it, and
optionally a name the band gave it) independent of, and outliving, any single part.
This is exactly the deferred idea already on file about items remembering a previous
owner: this section is where it attaches, once assemblies are instances at all
(section 5, step 4).

This also answers, in the simpler direction, whether resource nodes need individual
material identity (former open question): they do not. A felled tree does not need
its own tracked properties for this to work — ordinary per-resource-kind material
(section 1) is enough, because the identity a player cares about ("father's axe") is
carried by the *assembly instance*, not inherited from which literal trunk the wood
came from. Per-tree property variation stays a possible future refinement
(`ResourceDefinition.YieldsItem` today assumes the simpler shape), not a prerequisite.

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

### Player path UI: a workshop panel, not a recipe menu

A dedicated panel, opened from the selected person's card ("Workshop"), rather than
folding this into the selection card's existing action list. Combining reaches across
everything a person carries, and a single-item verb (squeezing an apple, twisting
grass) still needs its own screen real estate for the outcome - neither fits the
one-button-per-row shape `ActionList` already has.

Opening it holds the world clock exactly the way `PausePanel` already does (Main.cs) -
no new pause primitive, and the same "Space to resume" framing applies. This is a
deliberate choice, not just a courtesy: experimenting is meant to be unhurried
tinkering, not something to rush before the world moves on.

**No verb picker.** The player selects one carried item, or two, and presses a single
"try it" action - never `Twist`, `Crush`, `Bind` as a chosen option. The simulation
resolves which verb (if any) the selected item(s) afford from their properties, same
as every other knowledge blocker in this game hides rather than greys out (see
`ActionBlocker`'s circumstance/knowledge split): offering a verb list would let the
player read the solution off the menu, which is exactly the "craft axe" button this
plan exists to avoid, just wearing a longer list. It also means the panel's shape
never has to grow when a new verb is added - it stays two selection slots and one
button regardless of how much section 3's vocabulary grows.

The outcome is prose in the panel, not a new item that silently appears - "the cord
holds fast" or "it slips loose, wasting the grass" (section 9's legibility rule
applies here too: no numbers, no probability shown before the attempt).

### Skill level as a success modifier, not a gate

Whether an experiment is even attemptable is a property question (section 2): a
person who has never handled a hard material at all has nothing to reach for. But
whether a *sound* pairing succeeds should not be all-or-nothing on property gates
alone, or the first correct guess always works and skill has nothing to express.

The relevant `Skills` level (the skill whose actions handle the materials involved —
woodcutting for a wood-and-stone pairing, for instance) instead scales the **success
chance** of a directed experiment, the same way it already scales `GatherCommand`'s
harvest amount. A property-sound pairing (wedge + shaft) always has *some* chance at
level zero — a lucky novice can still bind an axe — but a practiced hand succeeds far
more reliably and wastes less material on a miss. This keeps the two failure modes
distinct: a property-unsound pairing (grass + water) is refused outright, a
property-sound one at low skill can still fail and cost the material.

Idle experimentation reads the same chance, since it runs the identical adjudication
with a random rather than a chosen pairing (section 7's three layers apply to both
paths). Skill level therefore also explains why a settled, practiced band invents
faster than a young one even before anyone directs an experiment on purpose.

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

**Decided:** property knowledge is modelled as a belief from the start, even while every
belief is transmitted perfectly and no distortion exists yet. Verbs themselves stay plain
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
7. Remaining verbs, each a small, flat addition rather than a new mechanism. Three of a
   verb's four facts are content — what it is called, which skill it is, and what each item
   it touches turns into (`FormTransition`) — and the fourth is a predicate over material
   properties, which is code because it is physics rather than authoring (section 2). The
   machinery they share is written once (`ReductiveWork`), so what a new verb adds is those
   four facts and nothing else. *Originally written as "as content, not code"; step 4c-10
   showed that overstated it.*

Steps 1-3 can be done without touching discovery at all, and on their own they retire the
tool whitelist.

---

## 12. Open questions

None remaining as of 2026-09-19 — form transitions (section 3), quality and assembly
identity (section 6) and function-score wording (section 4) are all resolved above.
New questions belong here as they turn up.
