# Of Folk and Many Winters — Initial Development Plan

## 1. Project Direction

**Of Folk and Many Winters** is a single-player real-time settlement and society simulation focused on individual people, generational continuity, accumulated knowledge, and a world that visibly remembers its own history.

The first goal is not to build a large-scale civilization simulator immediately. The first goal is to create a small but complete simulation in which a group of hunter-gatherers can survive, learn, teach, build, age, and die across multiple winters.

A massive persistent multiplayer society remains a possible side experiment, but it must not complicate or compromise the first playable version.

---

## 2. Core Design Principles

### 2.1 Every Person Is an Individual

Population is never represented as an anonymous aggregate.

Every person is a persistent simulation entity with their own identity and history. Even when a settlement grows large, optimization should come from reduced simulation frequency, smarter scheduling, or level-of-detail techniques — not from replacing people with abstract population numbers.

Each person may eventually track:

- identity and name,
- age and life stage,
- parents, children, and family relationships,
- health and injuries,
- needs,
- skills and experience,
- known techniques and knowledge,
- occupation and current tasks,
- equipment and possessions,
- social relationships,
- personal history.

Not all of these systems need to exist in the first prototype, but the data model should not prevent them.

### 2.2 Micro Control Always Remains Available

The player must always be able to select and directly control an individual person.

Early gameplay will naturally involve a large amount of micromanagement because the group is small. As the settlement grows, the player's role should gradually shift toward macromanagement through:

- work roles,
- priorities,
- groups,
- production targets,
- work areas,
- managers or leaders,
- automated routines.

Macromanagement does not replace individual simulation. It sits above it.

A high-level instruction must ultimately be carried out by concrete people.

### 2.3 The World Has Physical Continuity

Nothing in the world should visually transform merely because a technology has been unlocked.

Progress must happen through physical actions:

- construction,
- repair,
- replacement,
- renovation,
- expansion,
- demolition,
- rebuilding.

A cottage must not magically change into a new visual tier.

If a building becomes more advanced, workers must physically alter it. A wall may be rebuilt in stone, a new room may be added, the roof may be replaced, or an upper floor may be constructed.

The same principle applies to:

- houses,
- workshops,
- roads,
- tools,
- clothing,
- fortifications,
- fields,
- infrastructure.

This should allow settlements to develop genuine historical layers. Old buildings, roads, and neighborhoods may survive for generations and become organic historical centers inside later towns.

### 2.4 Every Instance Should Look Unique

Entities of the same type should remain recognizable as the same type, but no two instances should look exactly identical.

This applies to:

- people,
- buildings,
- tools,
- weapons,
- clothing,
- furniture,
- infrastructure.

The intended rule is:

> **Type defines recognizability; instance defines individuality.**

Variation should preferably be deterministic. Every entity receives a persistent visual seed that produces the same appearance after saving and loading.

Variation can come from:

- modular parts,
- proportions,
- procedural deformation,
- material variation,
- wear,
- damage,
- repairs,
- extensions,
- decals,
- age,
- craftsmanship.

Objects should also accumulate visual history over time.

### 2.5 Death Is Permanent

Death of an individual is irreversible.

The death of a person does not normally end the game, but the person, their labor, experience, relationships, and possibly unique knowledge are permanently lost.

The world should remember the dead through systems such as:

- graves,
- genealogies,
- historical records,
- inherited property,
- descendants,
- named places or buildings,
- memories and traditions.

The fundamental rule is:

> **People are mortal, death is irreversible, and the world remembers that they existed.**

If the entire community dies out, the current game ends.

---

## 3. Time and Simulation

The first version will be designed for **single-player**.

The simulation should maintain a strict distinction between:

1. **simulation time**,
2. **simulation speed**,
3. **rendering and animation**.

Gameplay logic should operate on simulation time rather than frame time.

The player should be able to:

- pause,
- play at normal speed,
- accelerate quiet periods significantly.

The exact relationship between real time and game time should remain configurable during development. It should be tuned through playtesting rather than decided permanently before the prototype exists.

The target experience is that:

- individual actions remain understandable at low speed,
- seasons matter,
- winters arrive often enough to shape gameplay,
- children can visibly grow into adults during a meaningful play session,
- multiple generations can emerge over a longer campaign.

### Future Multiplayer Constraint

If conventional multiplayer is explored later, the world should still have one shared simulation clock.

Local timelines for different players should be avoided because they create major problems for:

- travel,
- trade,
- combat,
- aging,
- causality,
- synchronization.

A small session-based multiplayer mode may eventually use shared or consensus-controlled simulation speed.

A massive persistent multiplayer world is explicitly treated as a separate experimental direction rather than a requirement for the main game.

---

## 4. World and Map

The long-term world should be based on **real European geography**, while local gameplay content can remain procedurally generated.

Real-world data can provide:

- terrain elevation,
- rivers,
- lakes,
- coastlines,
- broad landscape structure,
- climate-related inputs.

Modern roads, settlements, industrial areas, and present-day land use should not simply be copied into the game.

The real geography should act as a physical foundation rather than a snapshot of modern Europe.

Potential data sources include:

- Copernicus DEM,
- Copernicus land-cover datasets,
- OpenStreetMap,
- Natural Earth.

### Prototype Map

The first playable map only needs to cover a few square kilometers.

A practical initial target is approximately:

**3 km × 3 km**

The prototype area should ideally include:

- a river or stream,
- forest,
- open ground,
- hills or elevation changes,
- useful natural resource variation.

The full European world, continent-scale streaming, and large-scale travel should not be implemented yet.

---

## 5. Starting Society

The first playable community will begin as a small group of **hunter-gatherers**.

A suitable starting population is approximately:

**10–20 people**

The initial gameplay should focus on immediate survival:

- gathering food,
- hunting,
- finding or producing tools,
- maintaining warmth,
- finding shelter,
- avoiding injury,
- surviving weather,
- surviving winter.

The settlement should not begin with a complete building menu or an established economy.

Even basic construction techniques should represent learned knowledge.

---

## 6. Skills, Knowledge, and Technological Development

Technological development should not primarily behave like a conventional global research tree.

The simulation should distinguish between:

### Skill

An individual's practical ability at performing an activity.

Examples:

- hunting,
- tracking,
- knapping stone,
- woodworking,
- skinning,
- construction.

Skill improves through experience.

### Knowledge

A transferable understanding of how something can be done.

Examples:

- how to create a sharp stone tool,
- how to haft a tool,
- how to construct a shelter,
- how to preserve hides,
- how to build a better hearth.

A person may know a technique without being highly skilled at executing it.

Knowledge should exist in people rather than instantly appearing in a global technology database.

People can share knowledge through:

- working together,
- teaching,
- observation,
- social interaction,
- later possibly storytelling, apprenticeship, or formal education.

This creates the possibility that knowledge can be lost.

If the only person who knows an important technique dies before passing it on, the community may genuinely forget it.

Technological progress therefore becomes part of generational continuity rather than a simple permanent unlock list.

---

## 7. Initial Knowledge Progression

The first prototype should contain a small but meaningful network of discoveries and techniques.

A rough early progression might include:

- gathering,
- basic shelter,
- controlled fire,
- stone selection,
- stone knapping,
- cutting tools,
- woodworking,
- hafting,
- spear making,
- improved hunting,
- hide processing,
- basic clothing,
- improved shelter,
- storage.

This does not need to be a perfectly linear tree.

Discoveries may eventually come from:

- repeated practice,
- accumulated experience,
- experimentation,
- observation,
- teaching,
- environmental circumstances.

For the first version, the system can remain relatively simple while preserving the distinction between personal knowledge and personal skill.

---

## 8. Core Threats

The first prototype should use environmental and survival pressure rather than warfare as its primary challenge.

Initial threats:

- hunger,
- cold,
- severe weather,
- dangerous wildlife,
- injuries,
- infection or illness.

These systems should interact.

For example:

- bad weather reduces gathering efficiency,
- poor food supply causes risky hunting,
- hunting creates injury risk,
- untreated injuries may become infected,
- weakened people are more vulnerable to cold,
- losing a skilled hunter reduces future food security.

The goal is to create emergent problems rather than isolated status bars.

---

## 9. First Core Gameplay Loop

The initial gameplay loop should be approximately:

**Explore → Gather → Hunt → Survive → Learn → Teach → Improve → Prepare → Survive Winter → Continue the Group**

More concretely:

1. People search for food and useful materials.
2. The player assigns or directly controls work.
3. Individuals gain practical experience.
4. People discover or learn techniques.
5. Knowledge spreads through the group.
6. Better tools and shelters become possible.
7. Weather and resource pressure increase.
8. The group prepares for winter.
9. People may become injured, ill, or die.
10. Survivors carry knowledge and experience into the next season and eventually the next generation.

The primary design question for the prototype is:

> **Is it interesting to guide a small group of distinct people through repeated winters while watching their knowledge, relationships, possessions, and settlement accumulate history?**

---

## 10. First Playable Vertical Slice

The first vertical slice should deliberately remain small.

### World

- one real-world-inspired European map,
- approximately 3 × 3 km,
- terrain,
- water,
- forest,
- open land,
- basic natural resource distribution.

### Population

- approximately 10–20 individual people,
- persistent identities,
- age,
- basic health,
- hunger,
- temperature exposure,
- basic skill experience,
- individual knowledge.

### Player Interaction

- individual selection,
- direct orders,
- basic work assignment,
- basic priorities.

### Survival

- gathering,
- hunting,
- food consumption,
- fire,
- shelter,
- weather,
- seasonal change,
- winter,
- injuries,
- dangerous animals,
- death.

### Crafting and Construction

- a small number of tools,
- basic shelters,
- hearth/fire,
- simple storage,
- physical construction tasks.

### Knowledge

- small early knowledge network,
- learning through activity,
- sharing knowledge between people,
- possibility of losing knowledge through death.

### Time

- pause,
- several simulation speeds,
- configurable year and season duration.

### Persistence

- save/load,
- stable entity identities,
- deterministic visual variation where practical.

---

## 11. Explicitly Out of Scope for the First Prototype

The following systems should not block development of the initial slice:

- large-scale warfare,
- diplomacy,
- other civilizations,
- continent-scale travel,
- massive cities,
- advanced government,
- complex religion,
- advanced economics,
- large production chains,
- full genetics,
- deep personality simulation,
- multiplayer,
- persistent MMO simulation,
- full European streaming world.

They may influence architecture where necessary, but they should not be implemented prematurely.

---

## 12. Technical Architecture Priorities

The simulation core should remain as independent from presentation as practical.

Important early architectural goals:

- deterministic or controlled simulation behavior,
- stable unique entity IDs,
- simulation state independent from rendering,
- configurable simulation tick rate,
- configurable calendar speed,
- data-driven knowledge and crafting definitions,
- persistent visual seeds,
- save-game versioning,
- clear separation between simulation entities and Godot scene representation.

The codebase should allow headless simulation tests without requiring the full game renderer.

This will also leave open the possibility of experimenting later with:

- multiplayer,
- server-side simulation,
- large populations,
- accelerated offline simulation,
- the persistent-society side project.

---

## 13. Suggested Implementation Order

### Phase 1 — Simulation Skeleton

Implement:

- simulation clock,
- entity IDs,
- `Person`,
- position and movement,
- needs,
- task system,
- basic save/load.

Goal: people exist, move, receive tasks, and persist.

### Phase 2 — Environment and Survival

Implement:

- terrain import,
- resource nodes,
- gathering,
- food,
- hunger,
- temperature,
- weather,
- fire,
- basic shelter.

Goal: a small group can survive or die.

### Phase 3 — Skills and Knowledge

Implement:

- activity experience,
- skills,
- personal knowledge,
- knowledge prerequisites,
- teaching/sharing,
- first discoveries.

Goal: the group improves through lived experience rather than abstract research points.

### Phase 4 — Tools and Construction

Implement:

- item instances,
- crafting,
- tools,
- construction tasks,
- shelter components,
- repairs,
- deterministic visual variation.

Goal: people materially improve their environment.

### Phase 5 — Seasons and First Winter

Implement:

- seasonal resource changes,
- weather severity,
- winter preparation,
- clothing or insulation,
- survival pressure.

Goal: create the first complete gameplay cycle.

### Phase 6 — Life, Death, and Continuity

Implement:

- aging,
- permanent death,
- inheritance of possessions,
- graves or historical record,
- basic family relationships,
- knowledge loss through death.

Goal: make individual lives matter.

### Phase 7 — First Macromanagement Layer

Implement:

- work roles,
- priorities,
- work groups or zones,
- high-level resource requests.

Goal: demonstrate the transition from controlling a handful of people directly toward managing a growing society.

---

## 14. Longer-Term Direction

Once the first winter loop is genuinely fun, development can expand toward:

- family formation and reproduction,
- childhood and education,
- richer social relationships,
- more complex construction,
- farming,
- animal domestication,
- trade,
- specialized professions,
- metallurgy,
- settlement expansion,
- multiple settlements,
- conflict and warfare,
- governance,
- cultural development,
- long-term historical records,
- large-scale European geography.

The guiding principle should remain:

> **Do not add scale until the life of one person, one family, one building, and one winter is already interesting.**

---

## 15. Side Experiment: Persistent Society

A separate experimental branch may later explore a massively multiplayer or always-running society simulation.

In that model:

- the world continues while players are offline,
- settlements rely heavily on autonomous behavior,
- time advances continuously,
- the player acts more as a long-term governor than a constant RTS commander,
- generations may pass between login sessions.

This experiment should reuse the simulation model where possible, but it must not dictate the design of the main single-player game.

---

## Immediate Next Step

Build the smallest simulation that can answer one question:

> **Can ten distinct hunter-gatherers survive one winter through their own labor, accumulated experience, shared knowledge, and the player's decisions?**

Everything in the first prototype should serve that test.
