# Release Pipeline

## Status

Implemented and live. Every push to `main` that touches the shipped game runs the full
CI, exports a Windows build, boots it, and publishes it as a GitHub release. The
permanent download link is:

```
https://github.com/filda/many-winters/releases/latest/download/ManyWinters-win64.zip
```

One thing blocks the stated purpose — handing the zip to an alpha tester who does
nothing but unpack and double-click. The build is unsigned, and Smart App Control
refuses unsigned binaries outright (section 5). Everything else works.

---

## 1. Shape and the decisions behind it

**Continuous release, no manual tags.** Version is `v0.1.<github.run_number>`, the tag
is created by the workflow. Deliberately monotonic rather than a moving `alpha` tag,
because auto-update frameworks need an increasing semver to recognise "newer", and
that door should stay open.

**Not marked prerelease.** Only a non-prerelease shows up under `/releases/latest/`,
which is what makes the permanent link work. One link to hand out, always current.

**Zip, not an installer.** For alpha testing, unpacking beats installing: nothing to
uninstall, and one fewer unfamiliar dialog. An Inno Setup installer is maybe thirty
lines away if it is ever wanted.

**Self-contained.** The .NET runtime ships inside (~76 MB, 190 files). The alternative
saves ~40 MB of download and costs every tester a runtime install — not a trade worth
making for an alpha.

**Gated on `build-test` and the real E2E, not CodeQL.** CodeQL is a security scan; it should not be
able to hold up a build. The windowed E2E suite runs against the exported Windows build inside
`release-windows` (MW_E2E_GAME_EXE points at ManyWinters.exe) and is the release gate: a red E2E
fails `release-windows`, so `publish` cuts no release. It runs only on the game-change pushes that
cut a release, so it skips docs- or tests-only pushes and pull requests. It is Windows-only
(PrintWindow/PostMessage drives a real window), so the Linux export keeps only the headless smoke
test.

**Only game changes cut a release.** Changes to `src/ManyWinters.Core/`,
`src/ManyWinters.Godot/`, `Directory.Build.props`, `ManyWinters.sln` or
`.github/workflows/` qualify. Tests, tools, `art/` and `docs/` still run the full CI,
they just do not produce a build. The filter has to live at job level, not in `on:` —
a workflow-level `paths-ignore` would also skip the tests, which is the opposite of
what is wanted.

**Last ten releases are kept.** Roughly 68 MB each, one per game commit, so they need
pruning.

**Runner images are pinned, not `latest`.** `ubuntu-latest` starts moving to Ubuntu
26.04 on 2026-10-19 and warns on every job until it does, so the Ubuntu jobs name
`ubuntu-26.04` outright. It was rehearsed before the pin: on 26.04 the Godot export,
the incremental publish and the exported game's headless boot all pass, and the Godot
editor cached under 24.04 runs there unchanged. The price is a line that ages in
silence, so it has to be bumped by hand; `windows-latest` is still `latest` because
Windows has no equivalent move announced.

## 2. Things that cost a day to find out

Recorded because none of them are guessable, and all of them will look like arbitrary
choices to whoever reads `ci.yml` next.

**Godot's Windows build must have its own `.sln`.** The export looks for a solution
inside the Godot project directory, named after the assembly:
`src/ManyWinters.Godot/ManyWinters.Godot.sln`. The root `ManyWinters.sln` does not
satisfy it. Nothing notices in the editor, because there the assembly is built by the
IDE and Godot only loads it; the export wants to publish it itself. Without that file
the export writes a launchable `.exe` containing no C# at all.

**Use the console binary in CI.** `Godot_v*_win64.exe` is a GUI-subsystem binary whose
stderr is swallowed on Windows. Run it and a failing export prints nothing but the
version banner. `..._console.exe` prints the actual error and returns a real exit code.

**Godot's exit code means nothing.** A run that fails to publish any C# still exits 0
and reports success. The export step therefore verifies artefacts — the `.exe`, the
`data_*` folder, `ManyWinters.Godot.dll` — and greps the captured output for `ERROR:`.

**A green export is not a working game.** Content that resolves under the editor's
loose files does not resolve inside a `.pck` — that is what `ContentFiles.cs` exists
for. The smoke test boots the exported build headless for 120 frames
and fails on any startup error. It caught exactly that class of bug on its first run,
before a broken zip reached anyone.

**rcedit writes the icon and version metadata.** Godot shells out to it on Windows.
Without it the export still succeeds, just with the stock Godot icon.

**The export template archive is 1.1 GB for every platform.** Only
`windows_release_x86_64*` plus `version.txt` and `icudt_godot.dat` are kept; the last
is shared ICU data rather than a platform binary and deleting it breaks the export.
The cache key carries a `-v1` suffix precisely so that changing those trim rules
invalidates it — otherwise a cache saved under the old rules is reused silently and
surfaces as a mysterious export failure.

## 3. Export time: ten minutes of waiting on a pipe

Solved on 2026-09-21: the export did its work in 28 s and spent the other ten minutes
waiting for a pipe nobody was going to close. `release-windows` now sets
`UseSharedCompilation` to false for the export step and takes ~21 s instead of ~625 s.
The trail below is kept because the wrong answers cost more than the right one.

The export step took ~620 s and was very stable (616–634 s across many runs). It was
effectively independent of codebase size: over a period in which `src/` grew by ~3,700
lines the number did not move. The output explains why — the game's own code and
content are ~0.4 % of what the step moves:

| part                | size    | whose            |
| ------------------- | ------- | ---------------- |
| `ManyWinters.exe`   | 104 MB  | Godot engine     |
| `data_*/`           | ~76 MB  | .NET runtime     |
| `ManyWinters.pck`   | 0.75 MB | our content      |
| our assemblies      | 0.24 MB | our code         |

Three hypotheses were tested and all three failed:

1. **NuGet downloads dominate it.** A cache was added; it restores 163 MB in ~6 s and
   the export time did not change. The cache is still in place but has no measured
   benefit.
2. **A `dotnet restore` before the export warms it.** Measured against a control run
   in the same window: 620 s without, 616 s with. No effect.
3. **A failing diagnostic `dotnet publish` step warms it.** This one *correlated* —
   twice, ~11 s instead of ~620 s, and removing the step put the ten minutes straight
   back. But the step exited 1, produced zero files, and restored only one project in
   1.17 s, so what it actually did remains unexplained.

Parked deliberately at that point: it cost wall-clock, not money, and it would not
grow with the game. The export step reports its own duration (`godot exit code: 0
after Ns`), so a regression stays visible.

Picked up again in a throwaway manual workflow, `export-investigation.yml`, deleted
once it had done its job (`git log` has it). It answered the open questions without
making every push slower or less deterministic. One dispatch ran six controls side by
side:

- `windows-latest` as the current release environment,
- the same runner with a diagnostic `dotnet publish` warm-up in front of the export,
- the same runner with `application/modify_resources=false`,
- the same runner with `UseSharedCompilation=false`,
- `windows-2022` as a second Windows environment,
- `ubuntu-latest` as the Linux control, with its image selectable through an input,
  which is how the Ubuntu 26.04 pin above was rehearsed.

It also ran a direct incremental `dotnet publish` probe after the Godot export on
both platforms. Because that came after Godot had already published the same
project/configuration/RID, it was only a warm comparison point rather than a cold
measure of raw .NET publish throughput on the runner.

The first dispatch (run 35538619833, 2026-09-20) killed three more hypotheses:

4. **It is the runner image.** No: `windows-2022` took 631 s against
   `windows-latest`'s 623 s.
5. **It is Defender.** No: the runner reports `RealTimeProtectionEnabled: False`.
6. **It is .NET.** No: on that same runner, from cold, a full
   `dotnet publish -c ExportRelease -r win-x64` finished in 15.4 s, restore included.

It also placed the time *inside* the export. The smoke test prints a build tag taken
from the exported assembly's last-write time, and it read 21:26:23 for an export that
started at 21:25:58 and ended at 21:36:21 — our C# was compiled 25 s in and the other
~598 s happened after it. Godot's log names two phases (`dotnet_publish_project`,
`savepack`) and carries no clock, so the export step was made to tail the redirected
log and write `export.timeline.log` with an elapsed-time stamp per line. Godot writes
that file in blocks, so a stamp places a phase boundary rather than timing a single
line, which was all it took to say which phase held the ten minutes.

Two mechanics cost that dispatch its two most interesting cells:

- The warm-up publish is not broken any more. With the SDK pinned by `global.json` it
  exits 0 in ~15 s, and the step's guard — a `throw` for the publish unexpectedly
  succeeding — aborted the job before the export it exists to time. Either outcome is
  now simply recorded.
- The Linux job failed *after* a green 15 s export, on a diagnostic
  `find … | sort -nr | head -n 10`: `head` closes the pipe, `sort` dies of SIGPIPE and
  `pipefail` turns that into exit 2. `ci.yml` never carried that line, so the release
  was never at risk.

The second dispatch (run 35625196738, 2026-09-21) read that timeline and found the
export is not slow at all — it is *finished* and waiting:

| variant              | export | last line Godot printed | silence |
| -------------------- | ------ | ----------------------- | ------- |
| `latest`, control    | 627 s  | +28 s                   | 599 s   |
| `2022`, control      | 634 s  | +28 s                   | ~605 s  |
| `latest`, no-rcedit  | 628 s  | +29 s                   | ~599 s  |
| `latest`, warmup     | 19 s   | +18 s                   | 1 s     |

7. **It is rcedit.** No: with `application/modify_resources=false` the export still
   took 628 s.

So Godot logs `savepack` as done 28 s in and then takes another ten minutes to exit,
and 599 s is the Roslyn compiler server's idle timeout to the second. The mechanism
that fits every observation: Godot spawns `dotnet publish` with its output redirected,
the `VBCSCompiler` that publish starts inherits the write end of that pipe and lingers
for its idle timeout, and Godot waits for an EOF that cannot arrive until the last
holder of the pipe exits. It explains hypothesis 3 as well — a warm-up publish starts
the compiler server under a different parent, so Godot's pipe is never inherited,
which is also why a warm-up that *failed* worked and why `dotnet restore`, which never
starts Roslyn, did not. Linux is unaffected because there Godot waits on the child
process rather than on the pipe.

The third dispatch (run 35628078891, 2026-09-21) confirmed it. Setting
`UseSharedCompilation` to false in the export step's environment — MSBuild reads
properties from there, so it reaches the publish Godot runs for itself — took the
export to **21 s** against 625 s for the control in the same dispatch, with the warm-up
variant at 22 s and `no-rcedit` at 619 s. `ci.yml` carries those two lines now. The
alternative, shipping the warm-up publish, works just as well but treats the symptom
and adds a step.

What made this expensive to find: every phase Godot names had already finished, the
exit code was 0, the output was complete and correct, and the ten minutes sat after the
last line of a log that carries no clock. Nothing was slow — something had already
ended and nobody noticed. The lesson worth keeping is the instrument rather than the
answer: stamping a child process's output with arrival times cost a dozen lines and
turned an unfalsifiable "Windows is slow" into a bounded question.

## 4. Asset growth is the cost that will actually rise

`.godot/` is not cached between CI runs, so every run re-imports every asset from
scratch. Today that is the `Import assets` step at 13–30 s. Two things will push it up:

- **SVG sprites.** Godot rasterises SVG at import time via ThorVG; the runtime cost is
  zero but the import cost is real and scales with asset count.
- **Audio.** Same import path, and there will be a lot of it (see
  `docs/audio-architecture.md`).

The fix, when it is needed, is a content-hash-keyed cache of `.godot/imported/`. Unlike
section 3 this one is understood and predictable, so it can wait until the number
justifies it.

## 5. Open: code signing

The blocking item. The zip is unsigned, which means:

- **Smart App Control blocks it outright.** No "Run anyway". SAC first asks its cloud
  service about the binary, and failing that checks for a valid signature from a CA in
  the Microsoft Trusted Root Program. Unsigned means blocked. Confirmed on a real
  tester machine.
- **SmartScreen warns** on every download. Dismissable, but it reads as "this is
  malware" to anyone who has not been warned.

SAC only runs on Windows 11 and mostly on clean installs; upgraded machines and
Windows 10 do not have it. It can be switched off *and back on* without reinstalling
Windows — an older restriction that has since been lifted — so telling a tester to
disable it is a legitimate stopgap, just not a distribution plan.

For SAC specifically, any valid signature from a trusted CA is enough; unlike
SmartScreen there is no reputation to build first.

**Route chosen: SignPath Foundation.** Free code signing for open-source projects, and
this project qualifies — OSI-approved licence (MIT), public repository, actively
maintained, and it already publishes releases in the form that would be signed. The
certificate is issued to "SignPath Foundation" rather than to an individual, which for
a personal project is a feature.

Rejected alternatives:

- **Azure Artifact Signing** ($9.99/month, no hardware token, clean CI integration)
  would be the first choice, but individual developers are limited to the USA and
  Canada. Organisations in the EU are eligible; routing a personal project through an
  employer is not wanted.
- **OV certificate**, $150–300/year plus an HSM or hardware token. The fallback if
  SignPath does not work out.
- **EV certificate.** No longer worth it — EV stopped bypassing SmartScreen in 2024
  and now behaves exactly like OV at more than twice the price.

When signing lands, it signs `ManyWinters.exe`, `ManyWinters.console.exe` and the DLLs
under `data_*/`, as a step between export and packaging, with the smoke test kept
*after* it so what gets tested is what gets shipped.

## 6. Open questions

- **Does the icon and version metadata actually land?** The rcedit step does not fail,
  but nobody has checked the exported `.exe`'s properties for `0.1.x.0` and the custom
  icon. Needs one download and a right-click.
- **Does signing change the export/packaging order?** A native GDExtension for audio
  (see the audio plan) would add a `.dll` that also needs signing.
- **Auto-update.** Wanted eventually, not now. The monotonic version scheme exists to
  keep Velopack or similar viable later.
