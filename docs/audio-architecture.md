# Audio Architecture — Design Exploration

## Status

Nothing implemented. There is no audio in the project at all: no `AudioStreamPlayer`
of any kind, no sound assets, no music. Everything below is design, arrived at by
working through what the game wants and what Godot 4 actually provides. The
engine-capability claims are checked against the documentation; the design decisions
are not validated by anything running.

One assumption is load-bearing and unverified — runtime soundfont switching
(section 2.3). It should be prototyped before any music is written, because the whole
music direction rests on it.

---

## 1. What the game wants

Two things, and they pull in different directions.

**Music that follows the age.** The game spans generations. The appealing idea is to
write melodies once and change their *instrumentation* by era, mood, weather — the
same tune on a bone flute in the first winter and on strings three ages later.

**A lot of specific, positioned sound.** Not just ambience: animals, people, tools.
Things happening at a place, heard from where the camera is.

The first is what MIDI is for. The second is a scheduling problem, because the world
already holds 10,647 resource nodes and that number will grow.

## 2. Music: MIDI plus soundfonts

### 2.1 Why it fits

A MIDI file contains notes, not sound. What those notes sound like is decided by the
soundfont at playback time. That separation is exactly the era/mood axis the game
wants, and it is a property of the format rather than something to be built.

The side benefits are larger than the headline. Because the score is data:

- individual tracks can be muted and unmuted (rain adds the drone),
- tempo can shift (winter plays slower),
- the whole thing can be transposed (famine goes minor),

all for a few lines, where pre-rendered audio would need one recording per variation.

And the size problem mostly disappears. Music is normally the largest audio cost in a
game — ten minutes of Ogg is 15–20 MB. Here the entire soundtrack, however long it
grows, costs a handful of soundfonts.

### 2.2 The discipline that makes or breaks it

A MIDI track does not say "play a flute", it says "play program 73". Which instrument
that is comes from the soundfont. So **every soundfont must share one program map**,
or the same melody plays the lead line on a drum kit in the wrong era.

That means deciding a project-wide slot convention up front — program 0 is the lead,
1 the drone, 2 percussion, 3 the pad — and authoring every soundfont against it. This
is authoring discipline, not code, and it is the thing most likely to be discovered
too late.

### 2.3 Switching: the unverified assumption

**No Godot MIDI addon documents changing the soundfont during playback.** They all
describe setting one before playback starts. The entire era/mood idea depends on this
working, and nobody claims it does.

Two designs sidestep it:

- **Banks instead of files.** One soundfont containing every era as separate banks,
  switched with `bank select` + `program change`. That is a plain MIDI event —
  instant, no loading, no reload risk. For this use case it is probably the better
  design regardless of whether file swapping works.
- **Two players and a crossfade.** Wanted anyway: an abrupt timbre change mid-phrase
  reads as a bug, a crossfade reads as intent. As a side effect it removes any
  requirement to swap a soundfont on a live player.

**Prototype this first** — one melody, two instrument sets — before any real music
exists.

### 2.4 Size

SF2 soundfonts can run to hundreds of megabytes, which would dominate the 68 MB
release zip. **SF3** is the same format with Ogg Vorbis compressed samples: MuseScore
took a soundfont from 140 MB to 12 MB that way, and `FluidR3_GM.sf3` is 19 MB. Those
are full General MIDI sets; a purpose-built set of a few instruments is far smaller.

So: keep sources as SF2, ship SF3. The compression is lossy, so SF3 is not an
archival format.

### 2.5 Player options, and the pipeline consequence

Godot 4 has **no built-in MIDI playback**. Its built-in MIDI support is input only —
keyboards and controllers. Playback needs an addon:

| option                | notes                                          |
| --------------------- | ---------------------------------------------- |
| Clef                  | SF2 synthesis, piano roll, Godot 4.6+           |
| SoundFont Player      | GDExtension, sf2/sf3/sfo                        |
| godot-midi            | MIDI parsing and event playback                 |
| FluidSynthGodot       | C# addon wrapping FluidSynth                    |
| pure-GDScript players | no native build, lower performance              |

`FluidSynthGodot` is worth a look specifically because it is C#, and would not bring a
second scripting language into a C#-only project.

Whichever native option wins, **the export gains a `.dll`** that has to be packaged
and, once code signing lands, signed alongside everything else. A pure-GDScript player
avoids that entirely, at a performance cost and by mixing GDScript into the project.

## 3. Sound effects

### 3.1 Format

WAV for short, repeated effects; Ogg Vorbis for long ambient beds. The reason is CPU,
not size: compressed formats decode on every playback, and the plan calls for many
sounds at once.

"WAV" does not mean "large" — Godot compresses on import. Per second of 44 kHz mono:

| import mode        | size  | note                                    |
| ------------------ | ----- | --------------------------------------- |
| PCM (uncompressed) | 88 KB | full quality                            |
| IMA ADPCM          | 22 KB | audible degradation                     |
| **QOA**            | 17 KB | clearly better than ADPCM, slightly more CPU |

QOA is the sensible default. Two hundred half-second effects land around 2 MB in the
`.pck` — invisible next to the 104 MB engine binary.

The cost that does bite is elsewhere: source WAVs live in git forever, and every CI run
re-imports them because `.godot/` is not cached. See section 4 of
`docs/release-pipeline.md`.

### 3.2 The concurrency budget

Godot documents no hard limit, but the practical guidance is **20–30 simultaneously
playing nodes** and `max_polyphony` between 1 and 4. That is a generous scene, but it
means something has to decide which sounds are the ones playing.

`AudioStreamPlayer3D` provides four attenuation models, doppler, `Area3D`-driven
reverb via `area_mask`, and `max_distance`, past which a sound is not mixed at all —
the cheapest culling available.

### 3.3 Why one player per entity does not work

Ten thousand entities each owning an `AudioStreamPlayer3D` is not an audio problem,
it is ten thousand scene-tree nodes. It falls over on node management long before
mixing.

The shape instead is a **pool**: a fixed number of players (say 32), lent out on
request. A request arrives — "axe, at this position" — a free player is moved there
and started, and returns to the pool when it finishes. When the pool is full the
**least important** sound is dropped, not the newest: a person dying outranks a bird
on the far side of the map.

### 3.4 It fits the presenter the project already has

`WorldPresenter` already consumes simulation events (`ResourceNodeAdded` and friends)
and turns them into Godot nodes. Sound is the same kind of work with a different
output. `ManyWinters.Core` keeps knowing nothing about Godot — it reports that a
person felled a tree at a position; the presentation layer decides whether that is
audible.

No new architectural layer, just another consumer of events that already exist.

### 3.5 Two categories, deliberately separated

**Discrete events** — axe, digging, speech, death. Real positions, a real pooled 3D
player each, and few enough at any moment.

**Ambient texture** — birds in a forest, insects, water. The tempting move is a sound
per tree, and it is a trap: ten thousand emitters of which twenty are audible, all
still managed. Instead, a handful of roaming emitters placed around the listener
according to the terrain it is standing in — in woodland, three birds appear at random
nearby points; near water, frogs. The player cannot tell the difference and there are
five nodes instead of thousands.

### 3.6 Listener placement is a real decision

`FreeCameraRig` zooms (R/F). Put `AudioListener3D` on the camera and zooming out makes
everything inaudible, because everything is far away. The usual answers are to place
the listener between the camera and its focus point, or to scale `unit_size` with
zoom. Either way it is tuned by ear, not derived.

## 4. Variation: the composite-sprite idea, applied to sound

The sprite pipeline already solves this problem visually, and the same moves transfer:

| sprites                                        | audio                                     |
| ---------------------------------------------- | ----------------------------------------- |
| layers `trunk` + `canopy` + `branches` + `fruit` | several samples played together          |
| variants `_v1`, `_v2`                          | `AudioStreamRandomizer` pool              |
| `EntityVisualVariation` (tint/scale from entity id) | pitch/volume offset from the same seed |
| `modulate`                                     | bus effects — filter, reverb, pitch shift |
| `art/generate_sprites.py`                      | generating sample variants at build time  |

**Start with `AudioStreamRandomizer`.** A pool of variants plus random pitch and volume
ranges. Three axe samples and ±5 % pitch already reads as endless variety, because the
ear looks for repeated patterns rather than individual differences.

**Then per-entity identity.** `EntityVisualVariation` derives tint and scale
deterministically from an entity id, so a given tree always looks the way it looks.
Its two general primitives are already the right shape for audio and need no changes:

```csharp
EntityVisualVariation.RangeFor(seed, PitchSalt, 0.95f, 1.05f)   // this entity's pitch
EntityVisualVariation.IndexFor(seed, VariantSalt, variantCount) // this entity's take
```

That gives a wolf its own voice — not random each time, *that* wolf's pitch. For a
game about a people across generations it is more than a trick: a person's voice comes
from their `PersonId` and disappears when they do. The salt parameter is what keeps a
person's pitch independent of their tint, exactly as it already keeps sprite layers
from varying in lockstep.

Caveat: `pitch_scale` on a player changes pitch **and speed** together, tape-style. At
±5 % that is exactly right and sounds organic; at large offsets it sounds like a
chipmunk. Pitch without tempo needs `AudioEffectPitchShift` on a bus, which costs CPU
and adds FFT latency.

**Then layering.** Decompose an event into impact, material and tail, each drawn from
its own variant pool with its own pitch offset. Three layers of three variants is 27
combinations from nine files, before any detuning. Structurally identical to building
a tree from a trunk and a canopy.

**Offline generation.** `art/generate_sprites.py` has an obvious sibling: a script that
generates sample variants at build time. Procedural where it is useful, paid once, and
an ordinary sample at runtime. This fits how the project already works.

**Runtime synthesis, if it is ever warranted.** `AudioStreamGenerator` with
`push_buffer()` hands over a raw PCM buffer. Notably, the documentation says it is best
used from C# or a compiled language — from GDScript the mix rate has to drop to 11 or
22 kHz to keep up — and this project is C#, which is the right side of that line. But
it means writing a synthesiser: a project in itself, and one generator per player, so
not a route to thirty concurrent voices.

## 5. Open questions

- **Runtime soundfont switching** (2.3). Undocumented in every addon. Load-bearing.
  Prototype before writing music. Bank switching is the fallback design and may be the
  better one anyway.
- **Listener placement under zoom** (3.6). Needs tuning by ear against the real camera.
- **Which MIDI addon.** Depends on 2.3 and on how much the C#-only property of
  `FluidSynthGodot` is worth against the maturity of the alternatives.
- **How many eras/moods**, and therefore how many instrument sets the program map has
  to serve. Affects authoring cost far more than runtime cost.
- **Synthesised effects from the music synth.** Once a soundfont synthesiser is in the
  project, a bird call is a short phrase on a flute preset — infinite variation,
  shared infrastructure. Whether it sounds characterful or cheap is unknown and only
  answerable by trying it.
- **Pool size and eviction policy** (3.3). 32 is a guess; the priority rule needs to be
  written down once there are real sounds competing.
