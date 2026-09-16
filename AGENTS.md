# Agent notes

Instructions for coding agents working in this repository. Read `docs/development.md` for build, run, test and editor workflows, and `docs/conventions.md` for engineering rules. This file records repository-specific traps only.

## Working style

- Do not commit. Leave changes in the working tree for the user to review.
- Run the repository gate before reporting work complete:
  `dotnet run --project build/ManyWinters.Build.csproj -- --target=CI`
- Keep LF line endings in files you create or edit.
- Do not add git hooks. Put checks in the Cake build, `Directory.Build.props`, `.editorconfig` or CI.
- Delete dead code instead of commenting it out. New production code needs tests in the same change.
- Use `git mv` for tracked renames and preserve unrelated work already present in the working tree.

## Cake build

Cake Frosting is the repository task runner. Its build project is under `build/` and is deliberately separate from `ManyWinters.sln`.

```text
dotnet run --project build/ManyWinters.Build.csproj -- --target=CI
dotnet run --project build/ManyWinters.Build.csproj -- --target=Beckett
```

Keep orchestration, process launching and log parsing in the build project rather than adding new PowerShell task scripts. Existing scripts are platform-specific fallbacks, not the primary build interface.

## Godot and Beckett

Beckett is an MCP server running inside the Godot editor. Prefer it for scene inspection, runtime state, screenshots and game interaction when it is available. Start it and wait for the server with the Cake target:

```text
dotnet run --project build/ManyWinters.Build.csproj -- --target=Beckett
```

If the tools still do not appear, the MCP client connected before the server existed and needs a reconnect. Do not start a second editor for the same project: Beckett may move to another port and make the client configuration stale.

The Godot editor rewrites `src/ManyWinters.Godot/project.godot` when it saves settings and removes comments. Put reasons for non-default settings in `docs/development.md`, not in that file. Editor addons are restored through `src/ManyWinters.Godot/plug.gd` and gd-plug; do not copy addon directories into the project by hand.

## Fallbacks when the editor is closed

- Godot-layer `GD.Print` output is in `%APPDATA%/Godot/app_userdata/Many Winters Godot/logs/godot.log`.
- After overwriting generated textures, run `godot --path src/ManyWinters.Godot --headless --import` before inspecting the game.
- On Windows, `scripts/screenshot-game.ps1 -OutFile shot.png` captures the game's own client surface when Beckett screenshots are unavailable.

## Project constraints

- Decorations created by `MapLoader.ScatterDecorations` and rendered by `TerrainRenderer` remain individual clickable `ResourceNode` entities. Do not replace them with `MultiMeshInstance3D` or another batching approach that removes per-node identity.
- Sprite art should follow the drawing in `docs/ZemanConceptArt.png`, not merely its motifs. Crop and enlarge the relevant reference region, preserve the repository palette and diagonal hatch fill, render a contact sheet at full and in-game scale, and inspect it before reporting the asset done.
