# Of Folk and Many Winters — Roadmap

This roadmap merges the design phases from `of-folk-and-many-winters-plan.md` (Section 13) with the technical steps from `Of Folk and Many Winters — Technical Implementation Plan.md` (Section 15) into a single execution order.

Guiding decision: the simulation core must stay independent from Godot at all times, but the Godot presentation layer should be connected early — right after the simulation skeleton exists — using deliberately ugly placeholder visuals (capsule/cube/cone), so that progress becomes visible as soon as possible instead of only after headless milestones.

---

## 1. Repository and Project Skeleton

- `OfFolk.Core`, `OfFolk.Godot`, `OfFolk.Tools`, `OfFolk.Tests` projects.
- Strict dependency direction: `OfFolk.Core` never references `OfFolk.Godot`.

## 2. Simulation Skeleton

- Simulation clock.
- Stable entity IDs.
- `Person` entity.
- Position and movement.
- Needs.
- Task system.
- Basic save/load.

Goal: people exist, move, receive tasks, and persist.

## 3. Headless Runner

- Console application (`OfFolk.Tools.SimulationRunner`) running the simulation without Godot.
- Basic commands: generate world, create N people, simulate N years, print population/food/discoveries.

Goal: the simulation can be tested and tuned independently of rendering, from the start.

## 4. Godot Connection (Placeholder Visuals)

- Camera, entity visualization using placeholder shapes (capsule/cube/cone/plane).
- Selection and inspection UI.
- Command layer introduced here (e.g. `AssignWorkCommand`) so the UI never mutates simulation state directly.

Goal: see the simulation running in the engine as early as possible, without committing to art direction.

## 5. Environment and Survival

- Terrain import.
- Resource nodes.
- Gathering.
- Hunger, temperature, weather.
- Fire.
- Basic shelter.

Goal: a small group can survive or die — now visible in Godot, not just in logs.

## 6. First Headless + Visual Milestone

- Validate "can 10–20 people survive one winter?" both headlessly and visually in the engine.

## 7. Skills and Knowledge

- Activity experience.
- Skills.
- Personal knowledge and prerequisites.
- Teaching/sharing.
- First discoveries.

Goal: the group improves through lived experience rather than abstract research points.

## 8. Tools and Construction

- Item instances.
- Crafting.
- Tools.
- Construction tasks.
- Repairs.
- Deterministic visual variation.

Goal: people materially improve their environment — meaningful now that it's visible in the engine.

## 9. Seasons and First Winter

- Seasonal resource changes.
- Weather severity.
- Winter preparation.
- Clothing/insulation.
- Survival pressure.

Goal: create the first complete gameplay cycle.

## 10. Life, Death, and Continuity

- Aging.
- Permanent death.
- Inheritance of possessions.
- Graves/historical record.
- Basic family relationships.
- Knowledge loss through death.

Goal: make individual lives matter.

## 11. Vertical Slice Evaluation

Go/no-go checkpoint. Test the core loop:

> Explore → Gather → Hunt → Survive → Learn → Teach → Improve → Prepare → Survive Winter → Continue the Group

Is it interesting to guide a small group of distinct people through repeated winters while watching their knowledge, relationships, possessions, and settlement accumulate history?

## 12. First Macromanagement Layer

- Work roles.
- Priorities.
- Work groups/zones.
- High-level resource requests.

Goal: demonstrate the transition from direct control toward managing a growing society.

---

## Explicitly Deferred

Not to be implemented before step 12 proves the core loop works, and not required at all for the first prototype:

- large-scale warfare, diplomacy, other civilizations,
- continent-scale travel, massive cities, advanced government,
- complex religion, advanced economics, large production chains,
- full genetics, deep personality simulation,
- multiplayer, persistent MMO simulation, full European streaming world,
- dedicated server, web backend, SQL database, authentication, cloud services,
- matchmaking, microservices, Docker, Kubernetes, complex ECS framework,
- final asset pipeline, final networking protocol, final save format.

These may influence architecture where cheap to keep open, but they must not block or complicate the steps above.
