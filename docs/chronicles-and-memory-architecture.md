# Chronicles and Memory Architecture — Design Exploration

## Status

Not implemented, not started, **deferred** — opened to hold the design so it is not lost.
Nothing here is decided. The one substantive conclusion below (section 1) is a recommendation
with reasons, not a settled decision.

Prerequisites split in two, and the split matters for sequencing:

- **Living memory and oral retelling** need `docs/knowledge-transmission-architecture.md`
  steps 1-4 — the belief representation and the per-hop distortion machinery. They do not
  need writing; people told each other stories long before they wrote them down.
- **Written chronicles** additionally need that plan's section 7 (writing, literacy, books).

So memory can exist as a mechanic well before chronicles do.

---

## 1. Per-nation or per-person? The dichotomy is false

The open question was whether the historical record is global per-nation or whether a
specific person's memory can be displayed. The useful answer is that **the global view can be
a read-model over per-person data rather than a second store**, and only that shape holds
together with the rest of the game.

Three reasons, in ascending order of force:

1. **The design already committed.** `of-folk-and-many-winters-plan.md`: "Knowledge should
   exist in people rather than instantly appearing in a global technology database." A global
   chronicle held as authoritative state contradicts that axis directly.
2. **The shape already exists in code.** `Grave` is exactly an artifact-based historical
   record: a detached snapshot (`Name`, `AgeAtDeath`, `CauseOfDeath`, parent names,
   `KnownTechniques`) at a `Position`, whose fidelity is gated by whether the burial was
   performed with the practiced technique (`IsMarked`). Graves are the game's historical
   record today, and they are neither global nor in anyone's head — they are **objects in the
   world**.
3. **Permaworld decides it.** The shipped "Another band comes" (`docs/status.md`, Step 10): after
   a nation dies out, a new band arrives into the same world. If history were per-nation state, it
   would vanish with the nation. **Only what is physically in the world survives an extinction.**
   The intended experience — a new band arriving into a landscape of graves and, later, unread
   books, with the history present but not yet recovered — is only possible if the record is
   artifacts.

### The resulting shape

- **Memory is per-person.** This is the primary, canonical store. A memoir is simply one
  person's memory, and yes, it should be displayable for a specific person.
- **History is artifacts in the world** — graves now, written chronicles later.
- **"The nation's chronicle" is an assembled view**, built at display time by querying
  people's memories, graves and written artifacts. It is never a store.

When two sources disagree, that is not a bug to reconcile — that is the game.

---

## 2. A memory is not a belief

These must not be conflated. They are different shapes and they decay differently.

| | Belief | Memory |
| --- | --- | --- |
| Shape | `(MaterialId, PropertyId, believedValue, confidence)` | an event: what happened, who was involved, when, where |
| About | how the world works | what happened |
| Timeless? | yes | no — anchored to a tick and a place |
| Distorts toward | a **wrong value** — noise | a **better story** — narrative |

The distortion difference is the interesting part. A belief drifts toward being numerically
wrong. A memory drifts toward being a better telling: exaggeration, misattribution to the
wrong person, and compression of five hard winters into "the Winter".

This is why **"stories" is a more precise word than "memoirs"** for what the mechanic
produces. Memory does not decay into noise; it **narrativises**. That is exactly what makes a
chronicle something other than an event log.

---

## 3. Forgetting is the mechanism, not housekeeping

Per-person memory of every event across many generations is unbounded, so it needs
compression. But compression is thematically free rather than a tax: what survives is what
mattered.

Retention should be **salience-based, not age-based**. An ordinary gathering trip from last
week is gone; the winter three generations ago that killed half the band is not. Age alone
would delete precisely the memories worth having.

---

## 4. Tiers of the record

From most volatile to most durable. Each tier is already a mechanic or is planned as one, so
this is a ladder rather than a new subsystem:

| Tier | Status | Fidelity behaviour |
| --- | --- | --- |
| Living memory | this plan | narrativises within one person's lifetime; dies with them |
| Oral retelling | transmission plan, steps 1-4 | distorts per hop, person to person |
| Graves | **exists** (`Grave`, `IsMarked`) | a fixed snapshot; either preserves identity or does not |
| Written chronicles | transmission plan §7 | stops per-hop decay, but **entrenches** whatever error it recorded, and lends it authority |

The written tier inherits the entrenchment property from the transmission plan: a wrong
chronicle is harder to dislodge than a wrong story, because the book outranks the reader's own
hands.

---

## 5. What the player sees

The "chronicle" screen is a query over the tiers in section 4, not a document the simulation
maintains. It inherits the transmission plan's legibility rule (its section 6): **show sources
and let the player notice divergence; never mark one as true.**

So the screen can legitimately show "Hrdla remembers it this way / the chronicle records it
that way" side by side, and leave the reader to decide.

There is a property here unique to this setup, and worth designing around rather than
stumbling over: **the player is the only holder of ground truth about history.** The player
watched the events actually happen. Every in-world record is therefore a degraded copy of
something the player already saw — which makes reading the chronicle an exercise in watching a
community misremember something you witnessed.

---

## 6. Risks

- **A write-only record.** The classic failure: history accumulates, is beautifully generated,
  and nobody reads it because nothing depends on it. The record has to feed back into
  gameplay — knowledge recovered from graves and books, reputations, decisions taken on
  remembered grievance — or it is flavour text with a save-size cost.
- **A second source of truth creeping in.** The moment anything caches "national history" as
  authoritative state, section 1 collapses. It has to stay a view, and that needs to be
  enforced, not merely intended.
- **Unbounded event log.** Mitigated by salience-based forgetting (section 3), which has to
  land with the memory store rather than after it.
- **Narrativisation producing mush.** Generating a readable story from event data is a content
  problem, and it is the same problem as generated item names in
  `docs/materials-and-crafting-architecture.md` section 8: an authored vocabulary plus
  recognised patterns, with generation only for the gaps. Worth solving once for both.
- **Save format churn**, again — a per-person event store on top of beliefs.

---

## 7. Open questions

- Does a per-person memory view exist in the UI as its own panel, or only as a source feeding
  the aggregated chronicle? Recommendation is the former, since per-person memory is the
  canonical store — but it is a real UI cost for something a player may rarely open.
- Do memories distort on retelling using the same per-hop machinery as beliefs, or does
  narrativisation need its own rules? The distortion *targets* differ (section 2), so the
  machinery may not transfer as cleanly as it looks.
- Is salience decided when the event happens, or re-evaluated later? An ordinary death that
  only becomes significant afterwards — the last person who knew a technique — argues for
  re-evaluation, which is much more expensive.
- Is memory self-serving? A person remembering their own part more favourably is very human,
  and it would make two eyewitness accounts differ without either being a transmission
  artefact.
- **Do grave records distort?** Already open in the transmission plan; it belongs to both
  documents and should be answered once.
- Can a chronicle record events its author never witnessed — hearsay history? If yes, written
  history can be wrong from the first copy, not merely from later ones.
- **Can a new band read a dead nation's chronicle and recover its techniques?** This is the
  one that decides whether history is a gameplay resource or decoration. It is also the
  strongest argument yet made for the artifact-based shape in section 1, and it interacts
  directly with permaworld.
- How do stories relate to the animals and other future actors — do only people remember?
  Presumably yes, but worth stating rather than assuming.
