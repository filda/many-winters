# Agent notes

Instructions for coding agents working in this repository. Everything about the project itself lives in `docs/development.md` (build, run, test, editor, repository tasks) and `docs/conventions.md` (engineering rules); read both before touching code. This file holds only what applies to an agent and to nobody else.

- Do not commit. Leave changes in the working tree for the user to review. Use `git mv` for tracked renames and say so, because it stages the rename. Another agent may be working in the same checkout: re-check `git status` before acting and leave changes you did not make alone.
- Before reporting work complete, run the repository gate and fix what it finds: `dotnet run --project build/ManyWinters.Build.csproj -- --target=CI`.
- Beckett, the MCP server inside the Godot editor, is the preferred way to look at scenes, runtime state and the running game. If its tools are missing or fail to connect, run the `Beckett` target (`--target=Beckett`); it starts the editor only when none is open and waits for the server. If the tools still do not appear, the MCP client connected before the server existed and the user has to reconnect it (Claude Code: `/mcp`). Say so instead of falling back silently.
