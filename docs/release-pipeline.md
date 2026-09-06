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

**Gated on `build-and-test`, not CodeQL.** CodeQL is a security scan; it should not be
able to hold up a build.

**Only game changes cut a release.** Changes to `src/ManyWinters.Core/`,
`src/ManyWinters.Godot/`, `Directory.Build.props`, `ManyWinters.sln` or
`.github/workflows/` qualify. Tests, tools, `art/` and `docs/` still run the full CI,
they just do not produce a build. The filter has to live at job level, not in `on:` —
a workflow-level `paths-ignore` would also skip the tests, which is the opposite of
what is wanted.

**Last ten releases are kept.** Roughly 68 MB each, one per game commit, so they need
pruning.

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

## 3. Export time: an unresolved mystery

The export step takes ~620 s and is very stable (616–629 s across many runs). It is
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

Parked deliberately. It costs wall-clock, not money, and it will not grow with the
game. The export step reports its own duration (`godot exit code: 0 after Ns`) so a
regression would be visible immediately.

If it is picked up again, the honest next step is to reproduce hypothesis 3 exactly —
including the broken `-p:BaseIntermediateOutputPath` override that caused it — purely
to establish whether the correlation is real. That override is what made the step
fail: Godot.NET.Sdk stops resolving `GodotSharp` and emits ~240 spurious `CS0246`
annotations against source files that are perfectly fine.

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
