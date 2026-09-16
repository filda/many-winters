# Agent notes

Instructions for coding agents working in this repository. Read `docs/development.md` for build, run, test and editor workflows, and `docs/conventions.md` for engineering rules. This file records repository-specific traps only. Where a rule can be checked or done by a tool, the tool is the rule: repository tasks are Cake Frosting targets under `build/`, run with `dotnet run --project build/ManyWinters.Build.csproj -- --target=<Name>`.

## Working style

- Do not commit. Leave changes in the working tree for the user to review. Use `git mv` for tracked renames and say so, because it stages the rename. Preserve unrelated work already present in the working tree.
- Run the `CI` target before reporting work complete. It is what `ci.yml` runs: line endings, restore, format check, Release build, InspectCode, tests. A green build alone misses unused `using` directives and dead members.
- Do not add git hooks. Put checks in the Cake build, `Directory.Build.props`, `.editorconfig` or `ci.yml`.
- Do not add shell scripts. Process launching, log parsing and Win32 calls go into the Cake build project as targets.

## Godot and Beckett

Beckett is an MCP server running inside the Godot editor. Prefer it for scene inspection, runtime state, screenshots and game interaction whenever it is available. It exists only while the editor is open on `src/ManyWinters.Godot`.

If the Beckett tools are missing or fail to connect, run the `Beckett` target. It starts the editor only when none is open on the project (a second editor makes Beckett walk to another port and every client config goes stale), waits for the server and writes the current URL into `.mcp.json` and `opencode.json`. If the tools still do not appear, the MCP client connected before the server existed and needs a reconnect (Claude Code: `/mcp`).

## Fallbacks when the editor is closed

- Godot-layer `GD.Print` output is in `%APPDATA%\Godot\app_userdata\ManyWinters Godot\logs\godot.log`; previous runs sit alongside as `godot<timestamp>.log`. Read it instead of asking for pasted console output. `Console.WriteLine` from `ManyWinters.Core` does not reliably reach it; put temporary prints in the Godot-layer caller.
- `godot --path src/ManyWinters.Godot` reads textures from the import cache, not the PNGs. After a generator overwrites a PNG, run `godot --path src/ManyWinters.Godot --headless --import` first. It prints a harmless "Unable to start the timer" error.
- The `Screenshot` target (`--out=shot.png`) saves the game window's own surface, so the game may be behind other windows. Do not replace it with `SetForegroundWindow` plus `CopyFromScreen`, which captured whichever window was really on top.

## Project constraints

- Trees, bushes, rocks, stumps, logs, grass, flowers and ferns placed by `MapLoader.ScatterDecorations` are individually clickable `ResourceNode` entities. Never batch them into `MultiMeshInstance3D` or any instancing that erases per-node identity; density problems are handled by lowering counts.
- Sprite art follows the drawing in `docs/ZemanConceptArt.png`, not merely its motifs. Crop and enlarge the relevant reference region, reproduce how it is drawn (silhouette construction, how hatching follows form), keep the repository palette and diagonal hatch fill, render a contact sheet at full and in-game scale (about 80 to 130 px), and look at it before reporting the asset done.
