# Audio synthesis prototype — plan

Executes section 5.7 of `docs/audio-architecture.md`. Standalone first: nothing here touches the Godot project until the listening gate (phase 4) passes. Each phase lists what it builds, what proves it, and who does it — mechanical phases go to a cheaper model with this file as the brief; design, review and the CI gate stay with the main model; ears are the user's.

## 0. Goal and exit

**Question.** Does a parametric synthesiser driven by the game's material properties produce impact sounds a listener can tell apart by material — and sound like the game, not like a toy?

**Exit A (pass).** Phase 5 integrates the synthesiser into the game behind the pooled-player design already in the architecture document.

**Exit B (fail).** After one day of tuning the blind ranking still fails. Impacts go to recorded samples; the synthesiser stays for ambient (wind), which does not need the gate. The architecture document records the outcome either way.

## 1. Shape

Three additions, all plain .NET, all in `ManyWinters.sln` so the existing `CI` target formats, builds, inspects and tests them without new build code.

| piece | path | references | purpose |
| --- | --- | --- | --- |
| library | `src/ManyWinters.Audio/` | none | DSP primitives, sound models, analysis helpers. `float[]` in, `float[]` out. No Godot, no Core. |
| tool | `src/ManyWinters.Tools/SynthPrototype/` | Audio | console app that renders the listening set into `artifacts/audio/` (already git-ignored) |
| tests | `src/ManyWinters.Tests/Audio/` | Audio | added to the existing test project, as `Tools/SimulationRunner` already is |

Plus one Cake target, `RenderAudio`, that runs the tool — repository tasks are Cake targets, not scripts.

**Why Audio does not reference Core.** The impact model needs three numbers, not a `MaterialDefinition`. Taking `ImpactMaterial(Hardness, Toughness, Density)` keeps the library a leaf that the tool, the tests and later the Godot project can all reference without dragging the simulation in. The one-line mapping from `MaterialDefinition` lives in the game (phase 5).

**Sample rate is a parameter**, never a constant: baked effects render at 22 050 Hz (half the bytes, plenty for impacts), the streamed wind runs at whatever the output device is.

**Two interfaces, no more.**

```csharp
// finite sounds: rendered whole, then played as a sample
float[] Render(int sampleRate, int seed);

// endless sounds: pulled a buffer at a time, parameters may change between pulls
interface ISampleSource
{
    int SampleRate { get; }
    void Read(Span<float> buffer);
}
```

The tool renders an `ISampleSource` to WAV by pulling it for N seconds; the game pushes the same buffers into `AudioStreamGenerator`. One code path, two consumers.

## 2. Phases

### Phase 0 — scaffold (cheaper model)

- Create the library, the tool and the test folder; add both projects to `ManyWinters.sln`.
- `WavWriter.Write(path, float[] mono, int sampleRate)` — 16-bit PCM, clipping to ±1.
- `Rng` — seeded xorshift; `NextFloat()` in [0,1), `NextGaussian()`.
- Cake target `RenderAudio` in `build/`, running the tool with `artifacts/audio` as output.
- Tests: WAV header fields and byte length; `Rng` determinism (same seed, same first 100 values).
- Gate: `--target=CI` green.

### Phase 1 — primitives (cheaper model)

Each primitive is a small class or static method with a test that checks a property, not a waveform.

| primitive | what | test |
| --- | --- | --- |
| `WhiteNoise` | uniform in [-1,1] from `Rng` | mean ≈ 0, RMS ≈ 0.577 over 1e5 samples |
| `PinkNoise` | Voss-McCartney or Paul Kellet filter | spectral centroid lower than white noise of same RMS |
| `Biquad` | low/band/high-pass, RBJ cookbook coefficients; `Retune` recomputes them in place, keeping the delay line, because wind sweeps its centre and a rebuilt filter restarts from silence and clicks | lowpass at 1 kHz attenuates a 4 kHz sine by > 20 dB and passes 200 Hz within 1 dB |
| `Envelope` | exponential decay with attack; `T60` stated in seconds | level at T60 within 10 % of −60 dB |
| `Oscillator` | sine and sawtooth, phase-accumulated, linear pitch glide | zero-crossing count of a 440 Hz sine over 1 s ≈ 880 |
| `ResonatorBank` | N damped two-pole resonators (frequency, T60, gain), driven by an input buffer | impulse response peaks at each configured frequency (FFT bin check); decays to −60 dB at T60 |
| `PoissonImpulses` | impulses at a mean rate, amplitudes from a distribution | count over 10 s within ±20 % of rate × 10 |
| `RandomWalkLfo` | bounded random walk with a rate in Hz, smoothed | stays inside bounds; consecutive-sample difference bounded by rate |
| `Analysis` | `Rms`, `Peak`, `Fft` (radix-2), `SpectralCentroid`, `DurationAbove(level)` | FFT of a pure sine peaks at its bin; centroid of a sine equals its frequency ±1 bin |

`Analysis` lives in the library, not in test support: the tool prints the same numbers next to each rendered file so tuning has numbers to look at, not only ears.

### Phase 2 — impact model (design: main model; code: cheaper model; tuning: user + main model)

`ImpactModel.Render(ImpactMaterial striker, ImpactMaterial struck, float size, int sampleRate, int seed)`.

Modal synthesis: a short noise burst excites two resonator banks (one per body), mixed 0.4 striker / 0.6 struck. Starting mapping — **these are the numbers tuning changes, not a specification**:

| parameter | formula (tuned 2026-09-27) | reason |
| --- | --- | --- |
| burst length | 15 ms − 9 ms × **mean** hardness of the pair | the two compliances add in series, so contact time belongs to the pair. The softer body's hardness alone gave stone-on-wood the same slow contact as wood-on-wood, which is what made it arrive as wood into a plastic barrel. A matched pair's mean is its own hardness, so this left the two approved sounds bit-identical |
| burst lowpass | 1 kHz + 7 kHz × hardness of the **harder** body | brightness comes from the body that does not give. Wood's own modes all sit below 2 kHz, so for a mixed pair this only colours the direct contact noise — and that glare is most of what says "stone" when the struck body does all the ringing |
| base frequency f0 | 110 Hz × √density × (1 + hardness)^2.5 / size | the exponent, not the base, does the separating: stone lands near 880 Hz and wood near 180 Hz, and pitch alone is now enough to name the material |
| mode ratios | 24 ratios, 1.00 … 11.60, crowded at the bottom, each ± `Rng` 3 % | sparse modes are a hollow vessel — the ear called six of them a saucepan and fourteen a plastic bucket. Neighbouring low modes beat instead of fusing, and no pitch is what says "solid" |
| quality factor Q | 3 + 25 × (1 − toughness) | toughness is loss, and loss is the thing that actually holds still across frequency |
| mode T60 | ln(1000) × Q / (π × f_mode), clamped to 5 ms … 800 ms | damping in proportion to each mode's own frequency, derived rather than fudged. **A stiffer body is higher and therefore shorter in seconds** — see below |
| mode gains | (index + 1)^−0.25, every mode but the fundamental × `Rng` 0.7–1.3 | a steep roll-off leaves one loud fundamental ringing alone once the transient has gone, which is exactly a struck plastic tube. The fundamental itself is not randomised: it carries the pitch the ear sorts by |
| striker Q | × 0.35 | a striker is held, and a hand is a large lossy mass against whatever it grips - it is why a bell is rung hanging and a hammer never sings. Ringing both bodies freely made a mixed pair two pitched objects sounding at once, heard as wood into a plastic barrel. The struck body still rings free |
| mix | 0.18 direct contact noise + 0.4 striker + 0.6 struck, normalised to peak 0.9 | for stone the noise is most of what says "two hard things met"; there is barely any ring left to carry it |

**The plan's own prediction was wrong, and the ear is what found it.** "Brittle and hard rings, tough thuds" is true in *oscillations* and false in *seconds*. Q counts cycles, so the same Q at a higher frequency is a shorter sound: stone's stiffness puts it near 880 Hz and a hand-sized stone cracks in about 15 ms, while wood at 180 Hz outlasts it at 40–55 ms despite being the lossier material. Two tuning rounds tried to keep stone ringing — half a second, then a fifth of a second — and both were heard as hollow plastic, because a lone decaying sinusoid *is* the sound of a struck tube. The discriminator the gate rests on is therefore **brightness, not length**: stone-on-stone 1.6–2.7 kHz at 9–18 ms, stone-on-wood 330–550 Hz at 27–56 ms, wood-on-wood 275–392 Hz at 27–39 ms.

Four rounds against the ear got there, and each one named the failure before the fix was findable: a saucepan (six sparse modes), a plastic bucket and a plastic pipe (a lone fundamental left ringing by frequency-proportional damping), then wood into a plastic barrel (a mixed pair given the soft body's slow contact, and both bodies left ringing free). Stone-on-stone and wood-on-wood were approved and held fixed from round three on — the mean-hardness contact and the held-striker damping were both chosen partly because a matched pair comes out unchanged under them, which made each later round a single-variable experiment.

With the shipped materials this gives stone (hardness 1, toughness 0.15, density 2) a bright ring of about half a second, and wood (0.4, 0.7, 0.5) a thud of about 120 ms. That is the *intended* direction; whether the ear agrees is what phase 4 asks.

**Tests.** Deterministic given seed. Every render normalised to the same peak. Stone-on-stone has a higher spectral centroid than wood-on-wood. Wood-on-wood `DurationAbove(−40 dB)` outlasts stone-on-stone, and a stiffer body of the same toughness is both higher and shorter. Doubling `size` roughly halves the dominant FFT bin.

**Tool output.** For each of stone-on-wood, stone-on-stone, wood-on-wood: five seeds, plus one file with all five concatenated at 300 ms spacing (the rhythm of work; repetition is where cheapness shows). A `report.txt` with RMS, peak, centroid and duration per file.

**Tuning loop.** `--target=RenderAudio`, listen, change a formula, repeat. Time-boxed to one day. Changes go into the mapping table above so the document stays true.

### Phase 3 — wind (cheaper model)

`WindSource : ISampleSource` with `Set(WindParameters p)` where `WindParameters(Strength 0..1, Gustiness 0..1)`.

Farnell's wind: white noise → bandpass whose centre (300–1 200 Hz) and gain follow a `RandomWalkLfo` at 0.1–0.5 Hz; a second, narrower band an octave up fades in with gusts for the whistle. Strength scales gain and LFO range; gustiness scales LFO rate.

**Tests.** `Read` fills the whole buffer, never exceeds ±1, and produces different output for `Strength 0.2` and `0.8` (RMS ordering). Parameter change between two `Read` calls does not click (no sample-to-sample jump above a threshold).

**Tool output.** Twenty seconds sweeping calm → storm → calm.

### Phase 3b — the rest of the verbs (main model designs; cheaper model implements; user tunes)

Phases 2 and 3 answered whether the synthesiser can carry *one* sound each. The question the impact model then raised is how much of the game one model covers, and the answer is that two more cover nearly all of it. The simulation's verbs group by physics, not by name:

| model | verbs it covers | built from |
| --- | --- | --- |
| impact (phase 2) | `KnapCommand`, `FellCommand` strikes, `DropCommand`, `DepositCommand`, `WithdrawCommand`, `PickUpItemCommand`, `RepairCommand` | `ResonatorBank`, `Biquad`, `WhiteNoise` |
| **granular** | footsteps (`MoveTask`, `FleeTask`, `FollowTask`), `BuryCommand`, `LootCommand`, `GatherCommand`, `TwistCommand`, `BindCommand` | `PoissonImpulses` + `ResonatorBank` + `Biquad` |
| **friction** | `SharpenCommand`, abrasion, later hide scraping | `Biquad` + `PoissonImpulses` |
| **ambient bed** | the seasonal layer that is simply always there — birds, insects, corvids, distant rustle, winter hush | `Oscillator` + `PoissonImpulses` + `GranularModel`, streamed |
| wind (phase 3) | weather, season | — |
| creak / stick-slip | the tree going over in `FellCommand`, a bound joint under load | `Oscillator` → `ResonatorBank` |
| liquid | stepping in water, and later rain and running water | three noise bands on separate decays |
| voice | `BirthCommand`, `TeachCommand`, `EatCommand` | deferred — a taste question, not a technical one (`audio-architecture.md` §5.3) |

**Water is not the granular model either, and not for the reason first supposed.** Three rounds of granular numbers were heard as banging on a gate and then as still not water. The first diagnosis was that water is tonal — Minnaert bubbles, gliding pitches, the textbook cue — and a bubble model was built. The ear threw the bubbles out: at any level where they were audible they read as a fizzing drink. What was actually missing is that a grain cloud has **one envelope over one texture**, where a splash is three bands ending at three different times: the slap of the surface, the spray pattering back, and the water moving underneath. The spray is impulses, not tones.

**Rustle is not a third model.** Twisted cord, pulled grass and dry leaves are the granular model with `ResonanceDamping` at 1: many small bright grains and no body under them. Building it separately would have been the same DSP with different numbers, which is what the parametric premise exists to avoid.

**`GranularSurface(GrainsPerSecond, GrainHardness, ResonanceHz, ResonanceDamping, NoiseWash)`** is what is underfoot; **`GranularGesture(AttackSeconds, T60Seconds, Intensity)`** is what was done to it. Orthogonal on purpose — the same soil is dug, walked on and scraped. Six surfaces are tuned: grass, soil, stone, snow, leaves, shallow water.

**`FrictionStroke(Grit, Pressure, StrokesPerSecond)`** renders a whole run of strokes rather than one, because a sharpening sound is judged on whether the strokes differ from each other.

**Tool output.** Three seeds of a single footstep per surface, plus an eight-step walk per surface at 0.55 s onsets with varied intensity — walking is where a granular model's repetition shows. Digging, rummaging, cord-twisting and grass-gathering once each. Three sharpening variants (coarse, fine, heavy pressure), five strokes each.

**Three things every model here needed, each found the hard way.** They are cheap to apply and expensive to rediscover, so they belong in the brief for anything built next rather than in one model's comments.

1. **A two-pole filter sheds only 6 dB per octave.** A bandpass on noise, or on impulses, leaves nearly all the top end standing, and the result reads as hiss whatever its centre says. Every model ends with a lowpass over the mix — and placed *clear of* the band it shapes, never on top of it: putting the splash's cutoff at its own spray centre cost a round and was heard as a spade going into soil.
2. **Peak normalisation alone buries a spiky signal.** Anything built from impulses has a huge crest factor — a measured 40 — so scaling its loudest spike to full scale puts the texture 30 dB down and leaves a few grains poking out of silence. Grass came back as "a ticking clock" from exactly that. Drive to a target RMS, soft-limit with `tanh`, then normalise the peak for a common reference.
3. **Vary every render or it sounds like a sample.** Filtered noise under a fixed envelope has no character of its own: three splashes once came back within 14 Hz of each other. Jitter band frequencies, decays and onsets from the seed. The same applies at the next level up — eight footfalls at exactly 0.55 s are a metronome, which is the *other* half of why grass ticked.

Creak and voice stay unbuilt. Creak needs its own tuning round and the felling cascade it belongs to; voice is waiting on a decision that is not the synthesiser's to make.

### Phase 3c — the ambient bed (main model designs; cheaper model implements; user tunes)

Everything before this fires on an event. The bed is the layer that is simply *there*, and the one a player hears for hours rather than for half a second.

**Streamed, not looped, and for a specific reason.** What changes between spring and winter is not timbre but **density**: winter does not mute the birds, it thins them. That is the rate of a Poisson process, a continuous parameter, and four pre-rendered seasonal loops cannot be crossfaded without the crossfade being heard, because each is one fixed realisation. Synthesised, a season change is a different number and is inaudible by construction. A loop long enough to go unnoticed also runs to minutes, and four of them is tens of megabytes of WAV in git — the cost §3.1 of the architecture names as the one that actually bites.

`AmbientSource : ISampleSource` with `Set(AmbientParameters(BirdDensity, InsectDensity, CorvidDensity, RustleDensity, Hush))`, a peer of `WindSource` rather than mixed into it: the wind was already tuned and passed, and Godot's buses give per-layer volume for free, so mixing in code would duplicate what the engine does well. Season maps to those five numbers in the game, not here — the library stays a leaf, the rule `ImpactMaterial` follows. **There is no time of day**: a tick is a fraction of a season, not an hour, so the bed is seasonal only.

Four voices scheduled as Poisson events into a capped pool of active sounds, plus the continuous hush. Events span buffer boundaries, which no finite model had to do; the test that proves the pool carries state is rendering the same thirty seconds in 512-sample and in 4096-sample chunks and asserting the two are identical.

**Birds have identity.** A small fixed set of `BirdVoice`s, so the same few birds recur rather than every call coming from a stranger — nothing stored, the identity is in the seed, exactly as `EntityVisualVariation` does it for sprites. It is the one deliberate exception to "vary every render": vary the phrase, not the voice.

**Winter's hush is a sound, not an absence** — a very quiet low bed with a slowly wandering level, so the emptiness reads as deliberate rather than as a layer that failed to load.

**What the voices taught, beyond the three lessons above.** These are the ones that will matter when voices for people are decided.

- **Two separate things destroy the pitch of an irregular pulse sequence, and the jitter is the smaller of them.** Measured, as the line at the sequence's own rate over the mean spectral bin: random per-pulse sign with a sixth of interval jitter gives 1.6×, one polarity with the same jitter gives 6.1×, one polarity with a half gives 2.1×. So the sign matters more than the jitter. `CreakModel` needs the random sign to keep a DC offset out of its direct mix and wants no pitch anyway; `CorvidCaw` inherited it and therefore has no surviving pitch either — what carries that call is the formant contour, the breath and the plateau, not the pitch its comments first claimed. `VoiceModel` needs a recoverable fundamental, so it uses one polarity and relies on its formants, which are bandpasses, to remove the DC.
- **A decay envelope is the envelope of a struck object**, and reads that way whatever is underneath it. A call holds and stops: quick on, plateau, quick off.
- **A contour that runs the whole length is a sweep.** Pitch falling evenly from the first sample to the last, with the formants sliding under it, is a laser — and was heard as one. The close belongs in the last third, where a beak actually shuts.
- **A pulse train alone is a buzz however it is filtered.** Breath through the *same* formants is what the ear takes for a throat; a hiss laid over the top is not the same thing.
- **Where the fundamental sits decides more than the formants do.** A corvid train at 130–250 Hz was heard as flatulence whatever was stacked above it. A crow is nearer 500 Hz, a raven 280.

### Phase 4 — listening gate (user)

The tool writes the impact files under neutral names (`a.wav` … `o.wav`) into `artifacts/audio/blind/`, and the mapping to `artifacts/audio/blind-key.json` — beside that folder, not in it, so opening it to play the files does not show the answers. The blind files get no line in `report.txt` either: their measurements are identical to the named renders they came from, so the set could otherwise be decoded by matching centroids instead of listening. The user, without the key, groups the fifteen files into three sets and names the material of each. Pass: grouping correct and at least two of three materials named right. Also a yes/no: "would this sit next to the woodcut sprites?"

Exit A or B as in section 0. Record the result in `docs/audio-architecture.md`.

### Phase 5 — game integration (only after Exit A; main model designs, cheaper model implements, user runs the game)

All in `src/ManyWinters.Godot/`, following the presenter pattern already in the architecture document.

1. **Reference** `ManyWinters.Audio` from the Godot project. It is managed code and ships as one more assembly beside `ManyWinters.Core.dll`; no native library, nothing new for signing.
2. **`AudioStreamWavFactory`** — `float[]` → `AudioStreamWav` (`Format16Bits`, mono, `MixRate` 22050). Ten lines, the only place Godot audio types meet the library.
3. **`SoundPool`** — 32 `AudioStreamPlayer3D` under one node; `Play(stream, position, priority)`; evicts the lowest-priority playing sound when full. Pure eviction logic extracted into a Godot-free class with tests.
4. **`WindLayer`** — one `AudioStreamPlayer` with an `AudioStreamGenerator` (`MixRateMode = Output`, `BufferLength` 0.15 s); `_Process` keeps the buffer topped up from `WindSource`; season → `WindParameters` mapping from `SeasonParameters`.
5. **One Core event.** The simulation has no "struck" event today; commands are one-shot. `FellCommand` is the right first case: it is the axe sound the architecture document keeps reaching for, and it already chooses the tool through `Inventory.BestChoppingScore`. Add `WorldState.Felled(Person actor, Entity node, ItemKind? tool)` raised from `FellCommand.Execute`, and a sibling of `BestChoppingScore` that returns the winning item rather than its score. Struck material: the tree's material via the resource's yielded item and `ItemCatalog`; striker: the tool's head material, or a "hand" material (soft, tough) when felling needs no tool. *Re-read `FellCommand` and `Inventory` before writing this; the crafting changes in the working tree may have moved them.*
6. **`SoundPresenter`** — subscribes to `Felled`, maps `MaterialDefinition` → `ImpactMaterial`, renders, plays through the pool at the node's position with the actor's `CreatureId` as seed salt (`EntityVisualVariation.RangeFor`) so the same person's swing has the same character.
7. **Listener** on `FreeCameraRig`, positioned between camera and focus point; tune `UnitSize` by ear against zoom. Tuning session with the user (launching the game needs their go-ahead each time).
8. Update `docs/audio-architecture.md`: mark section 5 as prototyped, replace "starting point" numbers with the tuned ones, close the open question.

## 3. Order and dependencies

```
                          ┌→ 2 impact ─┐
0 scaffold → 1 primitives ┼→ 3 wind    ├→ 4 gate → 5 integration
                          ├→ 3b granular + friction ─┤
                          └→ 3c ambient bed ──────────┘
```

Everything after phase 1 is independent and can run in parallel. Only the impact model is gated by phase 4: wind, granular and friction ship either way, because none of them rests on a listener naming a material.

## 4. Conventions that apply

- Tests land in the same change as the code they test; no dead code; LF line endings.
- No commits: leave the working tree for review, and re-check `git status` first — another agent works in this checkout.
- Run `dotnet run --project build/ManyWinters.Build.csproj -- --target=CI` before reporting a phase done. The gate's E2E step launches the game: for phases 0–3, which never touch the Godot project, `--target=Test` plus `--target=InspectCode` is the working loop and the full `CI` run is the hand-off check.
