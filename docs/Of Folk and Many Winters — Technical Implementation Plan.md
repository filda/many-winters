# Of Folk and Many Winters — Technical Implementation Plan

## 1. Current Direction

The initial implementation should be a **local desktop game** with no mandatory backend or online infrastructure.

The project should use:

- **Godot 4.x** as the game engine
- **C# / .NET** as the primary programming language
- A **strictly separated simulation core**
- Local save files
- Single-player as the first implementation target
- Architecture that does not prevent future multiplayer
- Rendering approach left open between 2D, 2.5D, and 3D until prototyping provides enough information

The current visual references range from the systemic clarity of **RimWorld** to the spatial presentation of **Banished** and **Foundation**.

The likely direction is a strategy game rendered in 3D or 2.5D while keeping most gameplay logic effectively two-dimensional.

---

## 2. Architectural Principle

The most important technical rule is:

> The simulation must not depend on Godot.

Godot should provide presentation, input, audio, UI, platform integration, and the application runtime.

The actual game world should live in ordinary C# code.

```text
Player Input
    │
    ▼
Command
    │
    ▼
Simulation Core
    │
    ├── validates command
    ├── updates world
    ├── advances simulation
    └── produces events/state changes
    │
    ▼
Godot Presentation
    │
    ├── rendering
    ├── animation
    ├── UI
    └── audio
```

A simulated person must therefore not be a `CharacterBody3D`, `Node3D`, or another Godot object.

Instead:

```csharp
public sealed class Person
{
    public PersonId Id { get; init; }
    public Position Position { get; set; }
    public Age Age { get; set; }
    public Health Health { get; set; }
    public HouseholdId Household { get; set; }
    public Activity Activity { get; set; }
}
```

The Godot layer may create a visual representation for that person, but the representation is not the person itself.

---

## 3. Proposed Repository Structure

```text
OfFolkAndManyWinters/
│
├── src/
│   │
│   ├── OfFolk.Core/
│   │   ├── World/
│   │   ├── Population/
│   │   ├── Households/
│   │   ├── Resources/
│   │   ├── Production/
│   │   ├── Construction/
│   │   ├── Knowledge/
│   │   ├── Combat/
│   │   ├── Environment/
│   │   ├── Time/
│   │   ├── Commands/
│   │   └── Events/
│   │
│   ├── OfFolk.Godot/
│   │   ├── Scenes/
│   │   ├── Rendering/
│   │   ├── Input/
│   │   ├── UI/
│   │   ├── Audio/
│   │   └── Assets/
│   │
│   ├── OfFolk.Tools/
│   │   ├── WorldGenerator/
│   │   ├── SimulationRunner/
│   │   ├── SaveInspector/
│   │   └── Benchmarks/
│   │
│   └── OfFolk.Tests/
│
├── design/
├── docs/
└── tools/
```

The exact structure can evolve, but dependency direction should remain strict:

```text
OfFolk.Core
    ▲
    │
OfFolk.Godot

OfFolk.Core
    ▲
    │
OfFolk.Tools

OfFolk.Core
    ▲
    │
OfFolk.Tests
```

`OfFolk.Core` must never reference `OfFolk.Godot`.

---

## 4. Simulation Model

The simulation should be designed around persistent world state and explicit changes to that state.

Initial major domains will likely include:

### Population

- Individual people
- Age
- Sex
- Health
- Hunger
- Fatigue
- Skills
- Experience
- Knowledge
- Relationships
- Household membership
- Occupation and current activity
- Birth
- Aging
- Death
- Permanent death

### Households and Society

- Families
- Shared resources
- Social relationships
- Knowledge transfer
- Group membership
- Leadership
- Cooperation and conflict

### Environment

- Terrain
- Vegetation
- Wild animals
- Natural resources
- Weather
- Seasons
- Temperature
- Environmental hazards

### Economy

- Gathering
- Hunting
- Storage
- Consumption
- Tools
- Production
- Construction
- Resource transportation

### Knowledge and Technology

The game should begin with extremely limited capabilities.

Even basic concepts may represent acquired knowledge:

- Better gathering methods
- Hunting techniques
- Toolmaking
- Shelters
- Fire
- Storage
- Food preservation
- Construction methods
- Agriculture
- Specialized crafts

Knowledge should belong primarily to people and societies rather than acting only as a conventional global technology tree.

### History

Important events should be recordable as part of the world's history.

Examples:

- births
- deaths
- construction
- destruction
- discoveries
- migrations
- battles
- exceptional winters
- famine
- leadership changes

This can later support chronicles, family histories, settlement history, and long-term player memory.

---

## 5. Commands Instead of Direct Manipulation

The presentation layer should not directly modify simulation state.

Instead of:

```text
UI → Person starts cutting tree
```

use:

```text
UI
↓
AssignWorkCommand
↓
Simulation
↓
Person activity changes
↓
Simulation event/state update
↓
Renderer updates
```

Example:

```csharp
public sealed record AssignWorkCommand(
    PersonId Person,
    WorkTargetId Target
);
```

This is useful even for a purely local game.

It also creates a natural path toward multiplayer later:

```text
Single-player:

UI
↓
Command
↓
Local Simulation
```

could eventually become:

```text
Multiplayer:

UI
↓
Command
↓
Network
↓
Authoritative Simulation
```

without changing the fundamental gameplay model.

---

## 6. Time and Simulation

The game should not assume that rendering frames and simulation steps are the same thing.

Rendering might run at:

```text
60 FPS
```

while the simulation could operate on a different clock.

For example:

```text
Rendering
60 updates / second

Simulation
5–20 updates / second
```

or use event-driven systems where appropriate.

This separation will become important when the world contains hundreds or thousands of people.

It also allows:

- pause
- multiple simulation speeds
- fast-forward
- deterministic tests
- headless simulation
- offline benchmarks

---

## 7. Headless Simulation

One of the first important tools should be a console application capable of running the game without Godot.

Example:

```text
dotnet run --project OfFolk.Tools.SimulationRunner
```

It should eventually support commands such as:

```text
generate world
create 20 people
simulate 10 years
print population graph
print deaths
print food production
print discoveries
```

The goal is to test whether the game systems produce interesting outcomes without requiring graphics.

This will be especially valuable for balancing long-term systems such as:

- reproduction
- mortality
- food economy
- population growth
- knowledge transfer
- seasonal survival
- resource depletion

---

## 8. Rendering Direction

The rendering style should remain intentionally undecided during the early prototype.

Possible directions include:

### Pure 2D

Closest to RimWorld.

Advantages:

- maximum readability
- inexpensive assets
- simple rendering
- potentially very large populations

### 2.5D

A largely two-dimensional simulation presented with depth.

Potentially a strong fit for the project.

### 3D Strategy View

Closest to Banished or Foundation.

Likely characteristics:

- fixed or constrained strategic camera
- real 3D terrain
- 3D buildings
- 3D vegetation
- dynamic weather
- snow
- lighting
- day/night cycle

Gameplay itself can still mostly operate on a two-dimensional surface.

This is currently the most interesting candidate, but it should be validated by prototype rather than committed to immediately.

---

## 9. Visual Variation

One of the project's core visual principles should be:

> Two objects of the same type should not necessarily look identical.

Examples:

- every cottage can have different proportions or materials
- buildings accumulate modifications
- repaired buildings look repaired
- extensions remain visible
- old structures remain visibly old
- reconstructed buildings preserve parts of their history
- people have individual appearance
- trees and natural objects vary

Upgrading a building should not simply replace every instance with a new standardized model.

A building might instead be:

```text
original structure
+
extension
+
new roof
+
repaired wall
+
later workshop
```

This allows settlements to develop visible historical layers and eventually form genuinely old districts.

The rendering architecture should eventually support this kind of procedural or modular variation.

---

## 10. Persistence

The first implementation should use local save files.

No database should be required.

A save should contain enough information to reconstruct the complete simulation state.

Likely requirements:

- versioned save format
- stable entity identifiers
- explicit serialization
- migration support between save versions

The exact serialization format can be decided later.

The important rule is that save data should serialize the **simulation model**, not Godot scene objects.

---

## 11. Multiplayer

Multiplayer should **not be implemented during the initial prototype**.

However, the architecture should avoid making it impossible.

Potential future models include:

### Host-based multiplayer

One player runs the authoritative simulation and others connect.

### Dedicated server

The same simulation core runs in a separate .NET server process.

### Persistent world

A continuously running simulation in which players control different groups, settlements, or peoples.

This is particularly interesting for the long-term concept, but it introduces major design consequences and should be evaluated after the single-player simulation becomes compelling.

No networking infrastructure is needed now.

---

## 12. Initial Technical Stack

```text
Engine
Godot 4.x

Language
C#

Runtime
.NET supported by the selected Godot version

Testing
xUnit or NUnit

Version control
Git

Large binary assets
Git LFS if required

Initial deployment
Desktop

Initial platforms
Windows first
Linux/macOS later if practical

Backend
None

Database
None

Networking
None initially
```

---

## 13. First Prototype

The first prototype should deliberately look bad.

Example visual assets:

```text
Human       capsule
Tree        cylinder + cone
House       cube
Stockpile   colored rectangle
Terrain     simple plane
```

The purpose is not to establish the art direction.

The prototype should answer:

> Is managing and observing a small group of individually simulated people interesting?

### Minimal World

- Small terrain
- Forest
- Basic resources
- Approximately 20 people
- Simple day/night or seasonal clock

### Minimal Needs

- Hunger
- Health
- Rest

### Minimal Activities

- Idle
- Gather
- Hunt
- Carry
- Eat
- Sleep
- Build

### Minimal Economy

```text
forest
↓
wood / food
↓
transport
↓
stockpile
↓
consumption / construction
```

### Minimal Construction

At least one constructible shelter or storage building.

### Minimal Population Lifecycle

- aging
- hunger
- injury/death
- possibly reproduction shortly afterward

### Minimal Player Interaction

- camera
- select person
- inspect person
- assign activity
- designate work
- construct building
- pause
- simulation speed control

---

## 14. First Headless Milestone

Before adding significant content, the simulation should be able to answer questions such as:

```text
Can 20 people survive one winter?

Can they survive ten winters?

How much food does a settlement need?

How quickly does population grow?

Can a bad winter collapse a settlement?

Can knowledge disappear when its last holder dies?

Can a settlement recover after losing several experienced people?
```

These are more valuable early tests than graphical polish.

---

## 15. Development Priorities

The initial priorities should be:

1. Establish repository and project boundaries.
2. Create the pure C# simulation library.
3. Implement world time and entity identifiers.
4. Add a minimal population model.
5. Add resources and gathering.
6. Add needs and consumption.
7. Add command processing.
8. Build a headless simulation runner.
9. Connect the simulation to Godot.
10. Implement camera and entity visualization.
11. Add selection and inspection UI.
12. Add construction.
13. Add seasons and winter survival.
14. Evaluate whether the core loop is compelling.
15. Only then expand toward advanced technology, social systems, combat, visual identity, and multiplayer.

---

## 16. Things We Intentionally Do Not Need Yet

Avoid premature infrastructure.

Not required for the prototype:

```text
Dedicated server
Web backend
SQL database
Authentication
Cloud services
Matchmaking
Microservices
Docker
Kubernetes
Complex ECS framework
Procedural art pipeline
Final asset pipeline
Final networking protocol
Final save format
```

They can be introduced when an actual requirement appears.

---

## 17. Current Working Decision

The current technical direction is therefore:

> **Godot + C#, local-first, with a pure engine-independent C# simulation core.**

This lets the project start as a straightforward desktop strategy game while preserving several future options:

```text
2D
2.5D
3D

Single-player
Host multiplayer
Dedicated multiplayer
Persistent multiplayer

Godot renderer
Alternative renderer
Headless server
Simulation tools
```

The immediate goal is not to solve all of those possibilities.

The goal is to make sure the architecture does not unnecessarily eliminate them before the game itself tells us which direction is worth pursuing.