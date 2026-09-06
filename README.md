# Of Folk and Many Winters

A real-time strategy game about the birth and long-term evolution of a single people.

The player starts with a small band of *Homo sapiens* somewhere on a real map of Europe, its surroundings initially hidden by fog-of-war. There is no fixed historical year, no predetermined nations, and no "eras of history" — the people's own history, calendar, lineages, cities, institutions, wars, and myths emerge only through play. The game can begin with a few people armed with a stone and a stick in a cave and, after many generations, grow into a vast society, all without the world ever resetting into a new scenario.

## The people

Each person is initially an individually controllable individual with abilities, experience, relationships, and their own memory. Through work, travel, trade, combat, or encounters with others, they gain information and pass it on — sometimes accurately, sometimes distorted as in a game of telephone.

As the population grows, direct control gradually shifts toward managing a society: people take on occupations and routines, groups organize themselves, and instead of handling every single task, the player increasingly sets priorities, roles, and long-term goals, while still retaining the ability to intervene in an individual character's life. The same world supports conflict ranging from a brawl among a few relatives in a field to battles between armies of thousands. Lineages can gain prestige, become nobility, or found dynasties, but the main continuity is the people themselves.

## What makes it different

- **Knowledge lives in people, not in a tech tree.** Technologies are not picked from a visible list. People know only parts of a central concept graph and can discover new connections between known phenomena; a combination of experience, need, and suitable knowledge can lead to an idea, an experiment, and eventually a new method. Knowledge spreads physically, person to person, and is later preserved by workshops, schools, guilds, armies, archives, and other emerging institutions — or lost when the last person who held it dies unburied and unremembered.
- **One continuous world, one continuous people.** There are no scenarios, campaigns, or age transitions. The band you start with and the society you end up with are the same lineage, and everything in between is recorded as it happens.
- **Real geography.** The map is real terrain from real elevation data, and every coordinate in the simulation is a real-world meter.
- **Individual lives matter.** People age, die, are buried (or not), and leave behind possessions, graves, and family. Whether a grave carries a name depends on whether anyone alive knew how to make one.
- **No win condition.** Success is recorded in the emerging history in the form of records and milestones, such as the oldest city, the greatest territorial extent, the longest-lasting lineage, or surviving many winters.

## Where it stands

The current build is an early vertical slice: a small band on a real 1 km × 1 km patch of terrain gathers food, cuts wood, crafts tools and clothing, builds and repairs a storage hut, learns and teaches skills, lives through seasons, ages, dies, and gets buried — with or without a marked grave. The simulation runs headlessly and is tested that way; Godot renders it with placeholder-grade pixel art.

The step-by-step engineering log is in [`docs/status.md`](docs/status.md); what comes next is in [`docs/roadmap.md`](docs/roadmap.md).

## Documentation

| Document | What it covers |
|---|---|
| [`docs/development.md`](docs/development.md) | Prerequisites, building, running the game and the headless runner, tests, mutation testing, formatting, inspections. |
| [`docs/status.md`](docs/status.md) | Detailed per-step progress log against the roadmap. |
| [`docs/roadmap.md`](docs/roadmap.md) | Execution order and what is explicitly deferred. |
| [`docs/of-folk-and-many-winters-plan.md`](docs/of-folk-and-many-winters-plan.md) | Game design plan. |
| [`docs/Of Folk and Many Winters — Technical Implementation Plan.md`](<docs/Of Folk and Many Winters — Technical Implementation Plan.md>) | Technical implementation plan and architecture. |
| [`docs/conventions.md`](docs/conventions.md) | Engineering conventions. |
| `docs/*-architecture.md` | Per-subsystem architecture notes (terrain, sprites, audio, materials, knowledge, chronicles). |

## Quick start

Requires the .NET 8 SDK and the .NET build of Godot 4.7 — see [`docs/development.md`](docs/development.md) for installation and the full set of commands.

```powershell
dotnet build
dotnet test
godot --path src/ManyWinters.Godot
```
