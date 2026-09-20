# Knowledge Transmission Architecture — Design Exploration

## Status

**Started 2026-09-20.** The prerequisite below is met, both correcting forces are in
place, and the first distortion has landed - see "What is implemented" at the end of this
section. The rest of the plan is still design rather than decision.

**Hard prerequisite:** `docs/materials-and-crafting-architecture.md` steps 1-5. Until
property knowledge is represented as a belief rather than a bare `TechniqueId`, there is
nothing for this design to distort (section 2). This plan cannot start before that one.

What already works, and is not this plan's problem:

- **Transmission itself.** `TeachCommand` (player-directed) and
  `WorldState.AutoTeachNearbyPeople` (autonomous, `CasualTeachingChancePerTick = 0.05`,
  raised to `0.3` for critical techniques) pass techniques between nearby people.
- **Loss on death.** A technique nobody else knows dies with its holder;
  `Grave.KnownTechniques` preserves a record when the burial was performed with the
  practiced technique (`Grave.IsMarked`).
- **Experience not transmitting.** `Skills` levels are raised only by a person's own
  actions, and every skill's `EfficientTechnique` is explicitly excluded from casual
  teaching. This is deliberate and this plan does not change it.

What is missing is the interesting middle: knowledge that **survives but arrives wrong**.

### What is implemented (2026-09-20)

Sections 1-6 are partly live, built in the order section 5 insists on - **correction
before corruption**, so the world was never in a decay-only state:

1. *Correcting forces first.* Handling a substance teaches it truly over about a season,
   and working it teaches it truly at once (`Beliefs`, `WorkAttempt.TeachesWhatItIs`).
   Reality is therefore already the ground truth that settles any argument.
2. *Then the lossy channel.* Talk between people standing together passes beliefs on
   (`WorldState.ShareWhatTheyKnow`), and a retelling strays from what the teller believes
   by up to `SimulationRules.HearsayDistortion`, narrowed to nothing by the teller's skill
   at teaching. Error is laid on what the teller already believed, so it accumulates along
   a chain (section 1, property 1) without any hop count.

What holds of sections 1-6 already: it copies rather than moves (property 3); it is
correctable by reality and first-hand work (property 4, section 5); nothing anywhere marks
a belief as wrong and the holder cannot tell (property 2, section 6) - the workbench simply
describes a substance as *the selected person* believes it to be, so two people disagreeing
is something the player notices rather than is told.

Still open here: a directed low-error channel for beliefs (section 4's "deliberate teaching
as a defence against drift" - `TeachCommand` still carries only techniques, so the player's
defence today is sending somebody to find out first-hand), distorted *procedure* beliefs
and recognition thresholds (section 2), consensus (section 5), and everything in section 7.
Vital knowledge cannot distort because techniques remain binary, which is section 3 holding
by construction rather than by a guard.

Writing, literacy and books are an intended part of this arc but sit much further out, with
one constraint that applies to the earliest steps here — see section 7.

---

## 1. What "broken telephone" has to mean mechanically

Four properties, and the middle two are the load-bearing ones:

1. **Error accumulates along a chain.** A teaches B teaches C teaches D, each hop adding
   error to an already-imperfect value. Not a single lossy event.
2. **The holder cannot tell.** Corrupted knowledge must be indistinguishable from correct
   knowledge *to the person holding it*, or the mechanic is just a visible debuff.
3. **It copies, it does not move.** A still knows the correct version; B now holds a wrong
   one. Two incompatible beliefs about the same material coexist in one settlement, and
   reality eventually adjudicates between them. This is where the emergent interest lives.
4. **It is correctable.** Contact with reality, or a better teacher, restores it.

Chain depth needs no `hopCount` field — it emerges from property 1, because each hop
distorts a value that was already distorted.

---

## 2. Only structured knowledge can be distorted

The representation decides whether this design is possible at all.

| Kind | Distortable? |
| --- | --- |
| Binary technique (`TechniqueId`) | **No.** A boolean cannot be wrong, only absent — and failure to transmit already exists (the roll fails). |
| Skill level (`float`) | Technically yes, but skill deliberately does not transmit, and "taught badly, so level 2 not 5" is only a slower ramp, not a wrong belief. |
| **Belief about a material property** — `(MaterialId, PropertyId, believedValue, confidence)` | **Yes**, in the interesting way: the value can be wrong and the holder cannot tell. |
| **Belief about procedure** — "cord is made by twisting grass" | **Yes**, and arguably richer: corrupt the pairing and you get "cord is made by twisting bark", or by *pounding* grass. |
| **Recognition thresholds** — "an axe head must be this heavy" | Yes; corrupt the threshold and people build useless axes. |

So the payload is beliefs about properties and about procedure. Binary techniques stay
binary and un-distortable, which is not a limitation but a guard — see the next section.

---

## 3. Vital knowledge must not distort

Distortion belongs on **elaborative** knowledge (which material, which verb, how heavy),
never on **vital** knowledge (how to eat, how to forage). If "how to eat" can arrive
corrupted, the game becomes cruel and unreadable, and failure stops being legible as
anything but a bug.

The distinction already exists in code: `AutoTeachNearbyPeople` separates
`criticalTechniques` (teaching, eating) with a six-fold higher transmission chance. The
same set is the natural place to express "and these never distort".

---

## 4. Where error enters

Per hop, distortion magnitude should come from:

- **The teacher's own accuracy.** Nobody teaches better than they know, so a student's
  error is at least the teacher's error. This is what makes property 1 work.
- **The teacher's teaching skill.** `Skills.Get(TeachCommand.TeachingSkill)` already exists
  and already gates range through `EfficientTechnique`. A practiced teacher transmits with
  less error.
- **Which channel it came through.** Directed `TeachCommand` transmits with low error;
  autonomous `AutoTeachNearbyPeople` — knowledge drifting around by itself — with more.

That last one is the gameplay payoff: **deliberate teaching becomes a defence against
drift.** The player gets a reason to order explicit lessons rather than trusting proximity,
and it costs the teacher's time to do so. The two existing channels already differ in
fidelity conceptually; this makes the difference mechanical.

---

## 5. Correction, and why it is built first

Without restoring forces, a settlement's knowledge decays monotonically into noise.

- **Reality.** Acting on a belief and failing corrects it, or at least lowers confidence.
  This is the main force and it is nearly free: the crafting system already adjudicates
  against true material properties. Someone who believes grass is stiff tries to make cord,
  fails, and updates.
- **First-hand observation.** Handling the material yourself yields the true value
  (crafting plan §7, layer 1). First-hand experience is the ground-truth injection point,
  and the only one.
- **Consensus** (optional, later). An outlier drifts toward what several others believe.
  Interesting, but it can equally *entrench* a shared error — arguably a feature, and a
  reason to defer it rather than assume it.

The equilibrium worth aiming for: knowledge stays accurate while people still practise it
first-hand, and drifts when it only ever gets passed along verbally. That makes knowledge
decay exactly when the settlement stops doing the thing — which is the right statement for
a game about generations and winters.

**Sequencing consequence: build the correction before the corruption.** Reality-correction
lands in an earlier change than distortion, so the system is never in a decay-only state,
not even briefly on a branch.

---

## 6. Legibility

Property 2 (the holder cannot tell) is in direct tension with the player needing to
understand why something failed. Resolution: separate what the *character* knows from what
the *player* can see.

- Show beliefs, never truth: "Hrdla believes grass is stiff." A player who knows better can
  see the belief is odd.
- Show confidence qualitatively — "is sure", "half-remembers" — never a number.
- **Do not mark a belief as wrong.** Let the player notice that two people disagree. That
  discrepancy *is* the broken-telephone experience, and spotting it is the play.

This fits the crafting plan's directed-experiment loop: the player forms a hypothesis about
who is right and sends someone to find out first-hand.

---

## 7. Writing and books (later)

Registered as intended direction; no design yet, and further out than everything above.

Writing is the technology that **breaks property 1** in section 1: error stops accumulating
per hop, because a written belief is copied rather than re-told. That makes literacy the
natural mid-game breakthrough of the whole knowledge arc — the settlement's great leap is not
a better axe, it is knowledge that stops decaying. It also gives knowledge a way to outlive
its holder that is not `Grave.KnownTechniques`.

It must not arrive as a switch that deletes the mechanics above. Three frictions keep it
interesting:

- **Literacy is itself a technique.** A book is useless to someone who cannot read, so the
  knowledge problem does not vanish — it moves. Literacy now has to survive the broken
  telephone, which is a pleasing recursion rather than an escape from it.
- **A book is a physical object made of a material**, so the medium's own properties decide
  how long knowledge lasts. `Flammability` is already in the materials plan, and so are
  durability and water resistance: books burn, rot, and get lost. Writing is therefore an
  *application* of the crafting system, not a parallel system beside it.
- **Writing preserves errors as faithfully as truths, and lends them authority.** This is the
  important one. A spoken wrong belief is cheap to correct — reality contradicts it and the
  holder updates (section 5). A written wrong belief is harder to dislodge, because the book
  outranks the reader's own hands. So writing does not solve the knowledge problem; it trades
  a **decay** problem for an **entrenchment** problem, and the knowledge system stays alive
  after literacy instead of being finished by it.

### What this constrains now

A belief must be **externalisable** — storable detached from the person holding it, so that
the same structure can sit in a book, not only in a `Person`. `Grave.KnownTechniques` already
demonstrates the shape: a detached snapshot rather than a live reference. Cheap to respect
now, expensive to retrofit later.

What people write down is taken up separately in
`docs/chronicles-and-memory-architecture.md`: books hold not only beliefs about how the world
works but memories of what happened, and those two decay differently.

---

## 8. Risks

- **Frustration engine.** Mitigated by section 3 — vital knowledge never distorts.
- **Noise floor.** Everything decays to garbage without correction. Mitigated by the
  sequencing rule in section 5.
- **Illegibility.** Failure a player cannot explain reads as a bug, not as a mechanic.
  Mitigated by section 6.
- **Save format churn.** Beliefs replace a flat `HashSet<TechniqueId>` with structured
  per-material data; `Grave.KnownTechniques` changes shape too. Larger saves, and migration.
- **No counterplay means no mechanic.** This only pays off if the player can *act* on
  divergent beliefs — order a lesson, send someone to verify first-hand. Without
  counterplay it is random punishment wearing a theme. The counterplay has to be designed
  in, not assumed to emerge.

---

## 9. Implementation order

1. Beliefs carry accuracy and confidence (partly delivered by the crafting plan already).
2. **Reality-correction:** first-hand action writes the true value and raises confidence.
3. Distortion on transmission, magnitude from teacher accuracy, teaching skill, and channel
   (section 4). Vital techniques exempt.
4. Player-facing belief and confidence display (section 6).
5. Optional, later: consensus drift; per-person traits (memory, credulity); distortion of
   procedure and thresholds as well as property values.
6. Much later, and needing its own design pass: writing, literacy and books (section 7).

Steps 1-2 are useful on their own — first-hand experience beating hearsay is already a
statement worth having, with no distortion in the game at all.

---

## 10. Open questions

- Does distortion apply to verb-material pairings and thresholds from the start, or only to
  property values first? Pairings are richer but produce more spectacular nonsense.
- Is first-hand knowledge immune to later verbal corruption — can being told something
  wrong overwrite what a person saw with their own hands? Probably confidence-gated, but
  "trusting a confident liar over your own experience" is also a real human behaviour and
  might be worth having.
- **Do graves distort?** Shared with `docs/chronicles-and-memory-architecture.md`, and to be
  answered once for both. `Grave.KnownTechniques` is a third channel, and knowledge recovered
  from a marked grave arriving degraded is thematically strong — oral history about a person
  nobody living met. Mechanically it is another fidelity tier to define.
- Do children inherit beliefs from parents at a different fidelity than casual teaching
  between adults? Family relationships are roadmap step 10.
- Should a person be able to hold *two* conflicting beliefs about the same property, or does
  a new belief always overwrite the old one? Holding both is more human and much harder to
  display.
- Once writing exists (section 7): **does copying a book distort?** Transcribing by hand is
  itself a hop, so error would accumulate per *copy* rather than per telling — a much slower
  clock, but the same mechanism, and it would mean an old book beats a fresh transcript.
