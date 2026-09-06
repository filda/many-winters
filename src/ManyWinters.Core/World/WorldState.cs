using ManyWinters.Core.Commands;
using ManyWinters.Core.Construction;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.Time;

namespace ManyWinters.Core.World;

public sealed class WorldState(WorldConfiguration configuration)
{
    // A floor, not a tuning knob - a building can't be in negative repair.
    private const float MinCondition = 0f;

    private readonly List<Person> _people = new();
    private readonly List<Person> _forebears = new();
    private readonly List<ResourceNode> _resourceNodes = new();
    private readonly List<Building> _buildings = new();
    private readonly List<Grave> _graves = new();
    private int _nextPersonId = 1;
    private int _nextResourceNodeId = 1;
    private int _nextBuildingId = 1;
    private int _nextGraveId = 1;

    public SimulationClock Clock { get; } = new();

    public ExplorationState Exploration { get; } = new();

    // What this world was built from and runs on (catalogs, calendar, tuning numbers) - fixed
    // for the world's lifetime, unlike everything else here. Not part of a save file.
    public WorldConfiguration Configuration { get; } = configuration;

    public IReadOnlyList<Person> People => _people;

    // People who died before the story began and only exist to be somebody's mother or father
    // (see Person.Mother) - full Person objects with names and ids from the same sequence as
    // everyone else, so a grave's "child of X" and a save file can refer to them like anyone,
    // but never in People: nothing simulates, draws, counts or clicks them.
    public IReadOnlyList<Person> Forebears => _forebears;

    public IReadOnlyList<ResourceNode> ResourceNodes => _resourceNodes;

    public IReadOnlyList<Building> Buildings => _buildings;

    public IReadOnlyList<Grave> Graves => _graves;

    // The id the next Add* call expects on the object it's handed - ids are the world's to
    // hand out (sequential, never reused), not the caller's to invent, so a caller builds
    // its object around this and Add* refuses anything else (see AddPerson).
    public PersonId NextPersonId => new(_nextPersonId);

    public ResourceNodeId NextResourceNodeId => new(_nextResourceNodeId);

    public BuildingId NextBuildingId => new(_nextBuildingId);

    public GraveId NextGraveId => new(_nextGraveId);

    public Season CurrentSeason => Configuration.Rules.SeasonAt(Clock.CurrentTick);

    public event Action<Person>? PersonAdded;

    public event Action<ResourceNode>? ResourceNodeAdded;

    public event Action<Building>? BuildingAdded;

    public event Action<Grave>? GraveAdded;

    // Add* take a finished object rather than building one - what a person/node/building/grave
    // is made of is the caller's business (SpawnPersonCommand, BuryCommand, ...), the world's
    // is only to keep the list, hand out the id and tell the presentation layer. The id has to
    // be exactly NextPersonId: anything else means the caller either invented one or built the
    // object before something else got added in between, and both would corrupt the sequence.
    public void AddPerson(Person person)
    {
        ClaimId(person.Id.Value, ref _nextPersonId, nameof(person));
        _people.Add(person);
        PersonAdded?.Invoke(person);
        RefreshExploration();
    }

    // No PersonAdded, no exploration refresh - a forebear isn't on the map (see Forebears), so
    // the presentation layer must never hear about one. Being dead is what makes it a forebear
    // rather than a person; a living one would be a person hidden from the simulation.
    public void AddForebear(Person forebear)
    {
        if (forebear.IsAlive)
        {
            throw new ArgumentException("A forebear died before the story began - a living person belongs in People.", nameof(forebear));
        }

        ClaimId(forebear.Id.Value, ref _nextPersonId, nameof(forebear));
        _forebears.Add(forebear);
    }

    public void AddResourceNode(ResourceNode node)
    {
        ClaimId(node.Id.Value, ref _nextResourceNodeId, nameof(node));
        _resourceNodes.Add(node);
        ResourceNodeAdded?.Invoke(node);
    }

    public void AddBuilding(Building building)
    {
        ClaimId(building.Id.Value, ref _nextBuildingId, nameof(building));
        _buildings.Add(building);
        BuildingAdded?.Invoke(building);
    }

    public void AddGrave(Grave grave)
    {
        ClaimId(grave.Id.Value, ref _nextGraveId, nameof(grave));
        _graves.Add(grave);
        GraveAdded?.Invoke(grave);
    }

    private static void ClaimId(int id, ref int nextId, string parameterName)
    {
        if (id != nextId)
        {
            throw new ArgumentException($"Expected id {nextId} (the world's next one), got {id}.", parameterName);
        }

        nextId++;
    }

    public void Execute(ICommand command) => command.Execute(this);

    public static double Distance(Position a, Position b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    // The one proximity test every "act on that thing" command shares (gather, fell, bury,
    // deposit, ...) - exactly at the limit still counts as within reach. `rangeMultiplier`
    // is for the rare case of a wider-than-normal reach (TeachCommand's efficient teacher).
    public bool IsWithinReach(Position a, Position b, float rangeMultiplier = 1f) =>
        Distance(a, b) <= Configuration.Rules.MaxInteractionDistance * rangeMultiplier;

    public long AgeInYears(Person person) => AgeInYearsAt(person, Clock.CurrentTick);

    // Age as of some other moment than now - a death tick, say (see BuryCommand).
    public long AgeInYearsAt(Person person, long tick) => (tick - person.BirthTick) / Configuration.Rules.TicksPerYear;

    public long AgeInSeasons(Person person) => (Clock.CurrentTick - person.BirthTick) / Configuration.Rules.TicksPerSeason;

    // How much this specific person can carry right now - varies by age (see CarryCapacity)
    // plus whatever gear (a basket, a bag, ...) they currently have on them (same "presence,
    // not count" convention as InsulationFor - carrying five baskets isn't five times the
    // bonus of carrying one).
    public float MaxCarryWeightFor(Person person)
    {
        var baseWeight = CarryCapacity.BaseWeightFor(AgeInYears(person), Configuration.Rules.MaxLifespanYears);
        var gearBonus = person.Inventory.Counts.Keys.Sum(Configuration.ItemCatalog.CarryCapacityBonusFor);
        return baseWeight + gearBonus;
    }

    public void Advance(long ticks)
    {
        var rules = Configuration.Rules;
        var seasonParameters = Configuration.SeasonParameters;
        var itemCatalog = Configuration.ItemCatalog;
        var resourceCatalog = Configuration.ResourceCatalog;

        var startTick = Clock.CurrentTick;
        Clock.Advance(ticks);

        for (var i = 0L; i < ticks; i++)
        {
            var currentTick = startTick + i + 1;
            var climate = seasonParameters.ClimateFor(rules.SeasonAt(startTick + i));
            var baseHungerMultiplier = seasonParameters.HungerMultiplierFor(climate);
            var regenMultiplier = seasonParameters.RegenMultiplierFor(climate);

            foreach (var person in _people)
            {
                if (!person.IsAlive)
                {
                    continue;
                }

                person.Tasks.Advance(person);
                // Nobody just stands frozen once they run out of orders. An empty queue used
                // to always mean plain wandering (IdleTask); it now means "go use whatever
                // skill this person already has, or seek out food if hungry and empty-handed"
                // (see DecideIdleTask) - falling back to wandering only if neither applies. A
                // real order (MoveCommand etc.) replaces this the moment one comes in, same as
                // it would replace any other task - this only ever revisits its own two
                // autonomous choices (idle/gather), never a player-issued one.
                // IdleGraceUntilTick (see GrantIdleGraceCommand) can buy a few extra ticks of
                // standing still first.
                if (currentTick >= person.IdleGraceUntilTick && ShouldReconsiderIdleTask(person))
                {
                    var decidedTask = DecideIdleTask(person);
                    // Keep the SAME IdleTask instance while the decision is still "just
                    // wander" - IdleTask carries its own per-instance state (anchor, current
                    // leg, pause countdown between legs), which replacing it every single
                    // tick would silently throw away even though nothing actually changed.
                    // Genuinely switching task type (to/from GatherTask) always interrupts.
                    if (decidedTask is not IdleTask || person.Tasks.Current is not IdleTask)
                    {
                        person.Tasks.Interrupt(decidedTask);
                    }
                }

                // Attempted every tick a gather order is active, not just once on arrival -
                // GatherTask only knows how to walk (see its own doc comment), so the actual
                // harvest happens here; GatherCommand's own distance check silently no-ops
                // this while still en route.
                if (person.Tasks.Current is GatherTask activeGather)
                {
                    new GatherCommand(person, activeGather.Target).Execute(this);
                }

                var insulation = person.Inventory.Counts.Keys.Sum(kind => itemCatalog.InsulationFor(kind));
                var hungerMultiplier = Math.Max(1f, baseHungerMultiplier - insulation);
                person.Needs.Hunger = Math.Min(person.Needs.Hunger + (rules.HungerPerTick * hungerMultiplier), rules.MaxHunger);
                TryAutoEat(person);

                var diedOfOldAge = AgeInYearsAt(person, currentTick) >= rules.MaxLifespanYears;
                if (person.Needs.Hunger >= rules.MaxHunger || diedOfOldAge)
                {
                    person.IsAlive = false;
                    person.DeathTick = currentTick;
                    person.CauseOfDeath = diedOfOldAge ? DeathCause.OldAge : DeathCause.Hunger;
                }
            }

            AutoTeachNearbyPeople(currentTick);
            ResolveCollisions();
            RefreshExploration();

            foreach (var node in _resourceNodes)
            {
                if (!node.IsAlive)
                {
                    continue;
                }

                var definition = resourceCatalog.Get(node.Kind);
                if (definition.IsInhospitable(climate))
                {
                    node.ColdStress += 1f;
                    if (node.ColdStress >= definition.TicksToWither)
                    {
                        node.IsAlive = false;
                        node.DeathTick = currentTick;
                        node.CauseOfDeath = ResourceDeathCause.Climate;
                    }

                    continue;
                }

                node.ColdStress = 0f;

                var regenPerTick = definition.RegenPerTick * regenMultiplier;
                node.RemainingAmount = Math.Min(node.MaxAmount, node.RemainingAmount + regenPerTick);
            }
        }

        foreach (var building in _buildings)
        {
            building.Condition = Math.Max(MinCondition, building.Condition - (rules.ConditionDecayPerTick * ticks));
        }
    }

    // Only ever revisits the two autonomous choices (idle/gather) - a player-issued task
    // (MoveTask from a direct MoveCommand, say) is left alone; IdleTask always gets a second
    // look (something better might now apply); GatherTask normally only when its own target
    // has stopped being worth working (dead, or drained until it regenerates), rather than
    // every tick - that would otherwise re-plan (and so re-approach) the same resource
    // continuously - *except* when hunger has become an emergency (see NeedsToSeekFoodUrgently):
    // a person who set off gathering wood far from camp, then ran out of food along the way,
    // has to be allowed to change their mind and go find something to eat instead of walking
    // the rest of that original errand while starving to death.
    private bool ShouldReconsiderIdleTask(Person person) => person.Tasks.Current switch
    {
        null => true,
        IdleTask => true,
        GatherTask gather => !IsWorthGathering(gather.Target) || NeedsToSeekFoodUrgently(person),
        _ => false,
    };

    private bool NeedsToSeekFoodUrgently(Person person) =>
        Configuration.SkillCatalog.Find(EatCommand.Skill) is { } eating
        && person.Needs.Hunger >= Configuration.Rules.HungerSeekFoodThreshold
        && person.KnownTechniques.Contains(eating.BaseTechnique)
        && !HasEdibleFood(person);

    private static bool IsWorthGathering(ResourceNode node) => node is { IsAlive: true, RemainingAmount: > 0f };

    // "Idle" now means "put whatever skill this person already has to use, or go find food if
    // hungry and empty-handed" (todo: "Pokud už má osoba v idle nějaký skill, tak by ho měl
    // použít") - plain wandering (IdleTask) is only the fallback once neither applies. Hunger
    // takes priority over an already-known skill (see SimulationRules.HungerSeekFoodThreshold).
    private PersonTask DecideIdleTask(Person person)
    {
        var reachDistance = Configuration.Rules.MaxInteractionDistance;
        // Knowing how to eat is what makes seeking food worth prioritizing over whatever else
        // this person knows - without it, gathering more food wouldn't help them anyway (see
        // EatCommand's own gate), so this falls through to the general search below.
        if (NeedsToSeekFoodUrgently(person))
        {
            // Being edible alone isn't enough - a resource this person never learned to gather
            // (foraging, say) is exactly as unreachable to them as one that doesn't exist.
            var foodNode = FindNearestGatherableResourceNode(person.Position, definition => IsFoodResource(definition) && IsKnownSkill(person, definition.Skill));
            if (foodNode is not null)
            {
                return new GatherTask(foodNode, reachDistance);
            }
        }

        // Nearest wins regardless of which known skill it needs - a closer resource this
        // person already knows how to work beats a farther one just because it happens to be
        // for a skill they've practiced more. IsKnownSkill checks against SkillDefinition's
        // BaseTechnique (see its own doc comment) - not against KnownTechniques directly,
        // since that set holds arbitrary techniques (eating/teaching included) rather than
        // being keyed by skill.
        var node = FindNearestGatherableResourceNode(person.Position, definition => IsKnownSkill(person, definition.Skill));
        if (node is not null)
        {
            return new GatherTask(node, reachDistance);
        }

        return new IdleTask();
    }

    private bool IsKnownSkill(Person person, SkillTypeId skill)
    {
        var definition = Configuration.SkillCatalog.Find(skill);
        return definition is not null && person.KnownTechniques.Contains(definition.BaseTechnique);
    }

    private bool IsFoodResource(ResourceDefinition definition) =>
        definition.YieldsItem is { } item && Configuration.ItemCatalog.HungerRestoredPerUnitFor(item) > 0f;

    private bool HasEdibleFood(Person person) =>
        person.Inventory.Counts.Any(kv => kv.Value > 0 && Configuration.ItemCatalog.HungerRestoredPerUnitFor(kv.Key) > 0f);

    // Depleted-but-alive nodes (RemainingAmount 0, still regenerating) are skipped rather than
    // sent to and stood next to - with thousands of decoration-turned-resource nodes usually
    // nearby (see MapLoader.ScatterDecorations), a fuller one of the same kind is normally
    // right there too.
    private ResourceNode? FindNearestGatherableResourceNode(Position from, Func<ResourceDefinition, bool> matches)
    {
        ResourceNode? nearest = null;
        var nearestDistance = double.MaxValue;
        foreach (var node in _resourceNodes)
        {
            if (node is not { IsAlive: true, RemainingAmount: > 0f } || !matches(Configuration.ResourceCatalog.Get(node.Kind)))
            {
                continue;
            }

            var distance = Distance(from, node.Position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = node;
            }
        }

        return nearestDistance <= Configuration.Rules.IdleSearchRadius ? nearest : null;
    }

    // "Later they teach each other" - once at least one person knows something (and knows how
    // to teach - see TeachCommand), anyone else nearby who doesn't know it yet may pick some of
    // it up automatically, no player action needed ("tichá pošta" - see
    // SimulationRules.CasualTeachingChancePerTick for why it's a per-tick roll). Every alive
    // pair is checked every tick - with the population sizes this game actually has (tens, not
    // thousands, of people), an O(n^2) pass here is negligible next to the resource-node work
    // Advance already does elsewhere.
    private void AutoTeachNearbyPeople(long currentTick)
    {
        var skillCatalog = Configuration.SkillCatalog;
        var rules = Configuration.Rules;

        // Find, not Get - a catalog that never registered "teaching" (most unit tests, a
        // deliberately minimal world) just means nobody could possibly teach anyone anything,
        // not a crash.
        if (skillCatalog.Find(TeachCommand.TeachingSkill) is not { } teachingDefinition)
        {
            return;
        }

        var teachingBaseTechnique = teachingDefinition.BaseTechnique;
        var efficientTechniques = skillCatalog.Definitions.Select(d => d.EfficientTechnique).ToHashSet();
        var criticalTechniques = new HashSet<TechniqueId> { teachingBaseTechnique };
        if (skillCatalog.Find(EatCommand.Skill) is { } eatingDefinition)
        {
            criticalTechniques.Add(eatingDefinition.BaseTechnique);
        }

        foreach (var teacher in _people)
        {
            if (!teacher.IsAlive || !teacher.KnownTechniques.Contains(teachingBaseTechnique))
            {
                continue;
            }

            foreach (var student in _people)
            {
                if (student == teacher || !student.IsAlive)
                {
                    continue;
                }

                TechniqueId? teachableTechnique = null;
                foreach (var technique in teacher.KnownTechniques)
                {
                    var chance = criticalTechniques.Contains(technique) ? rules.CasualTeachingChancePerTickForCriticalSkills : rules.CasualTeachingChancePerTick;
                    if (student.KnownTechniques.Contains(technique)
                        || efficientTechniques.Contains(technique)
                        || !PassesCasualTeachingRoll(teacher.Id, student.Id, technique, currentTick, chance))
                    {
                        continue;
                    }

                    teachableTechnique = technique;
                    break;
                }

                if (teachableTechnique is { } techniqueToTeach)
                {
                    new TeachCommand(teacher, student, techniqueToTeach).Execute(this);
                }
            }
        }
    }

    // Deterministic from the ids and the tick alone (same seeded-randomness style as
    // IdleTask.SeedFor) rather than a shared mutable Random - reproducible from the same
    // starting state without depending on call order between people.
    private static bool PassesCasualTeachingRoll(PersonId teacherId, PersonId studentId, TechniqueId technique, long tick, float chance)
    {
        var seed = CasualTeachingSeed(teacherId.Value, studentId.Value, technique.Value, tick);

        // Stryker disable once Equality: NextDouble() returning exactly `chance` has
        // probability zero, so < and <= are the same roll
        return new Random(seed).NextDouble() < chance;
    }

    private static int CasualTeachingSeed(int teacherId, int studentId, string technique, long tick)
    {
        var x = unchecked((uint)(teacherId * 73856093) ^ (uint)(studentId * 19349663) ^ (uint)(StableStringHash(technique) * 83492791) ^ ((uint)tick * 2654435761u));
        x = unchecked(((x >> 16) ^ x) * 0x45d9f3b);
        x = unchecked(((x >> 16) ^ x) * 0x45d9f3b);
        x = (x >> 16) ^ x;
        return unchecked((int)x);
    }

    // Not string.GetHashCode() - .NET randomizes that per process, which would make this roll
    // come out differently every run instead of being a stable property of this pair.
    private static int StableStringHash(string value)
    {
        var hash = 5381;
        foreach (var c in value)
        {
            hash = unchecked((hash * 33) ^ c);
        }

        return hash;
    }

    // Mirrors Main.cs's own manual "Eat" button (OnEatButtonPressed) - eats through whatever
    // food is on hand until no longer hungry or nothing edible is left, rather than requiring
    // a specific item to be named. Runs every tick regardless of what task is active (even a
    // player-issued one) - a starving person shouldn't have to wait for a free moment to eat
    // out of their own backpack.
    private void TryAutoEat(Person person)
    {
        foreach (var kind in person.Inventory.Counts.Keys.ToList())
        {
            // Stops walking the rest of the inventory once there's nothing left to satisfy.
            // Stryker disable once Equality,Statement: EatCommand refuses to do anything at
            // zero hunger anyway, so this only saves the remaining calls
            if (person.Needs.Hunger <= 0f)
            {
                break;
            }

            new EatCommand(person, kind).Execute(this);
        }
    }

    // MoveTask/IdleTask only ever aim at a destination, with no awareness of who/what else is
    // already there, so this untangles whatever overlap that produced after the fact, every
    // tick - same O(n^2)-over-people precedent as AutoTeachNearbyPeople. Every separation this
    // tick is computed against positions as they stood at the start of it (not updated
    // mid-pass) and summed into one push per person, only applied - clamped (see
    // SimulationRules.MaxCollisionPushPerTick) - at the end, so the order overlaps happen to be
    // discovered in can't itself bias the result.
    private void ResolveCollisions()
    {
        var personCollisionRadius = Configuration.Rules.PersonCollisionRadius;
        var maxPushPerTick = Configuration.Rules.MaxCollisionPushPerTick;
        var resourceCatalog = Configuration.ResourceCatalog;
        var pushes = new (double X, double Y)[_people.Count];

        for (var i = 0; i < _people.Count; i++)
        {
            var a = _people[i];
            if (!a.IsAlive)
            {
                continue;
            }

            for (var j = i + 1; j < _people.Count; j++)
            {
                var b = _people[j];
                if (!b.IsAlive || !TrySeparation(a.Position, b.Position, personCollisionRadius * 2f, out var pushX, out var pushY))
                {
                    continue;
                }

                pushes[i] = (pushes[i].X + (pushX / 2), pushes[i].Y + (pushY / 2));
                pushes[j] = (pushes[j].X - (pushX / 2), pushes[j].Y - (pushY / 2));
            }
        }

        for (var i = 0; i < _people.Count; i++)
        {
            var person = _people[i];
            if (!person.IsAlive)
            {
                continue;
            }

            var (pushX, pushY) = pushes[i];
            foreach (var node in _resourceNodes)
            {
                var collisionRadius = resourceCatalog.Get(node.Kind).CollisionRadius;
                if (!node.IsAlive
                    || collisionRadius <= 0f
                    || !TrySeparation(person.Position, node.Position, personCollisionRadius + collisionRadius, out var nodePushX, out var nodePushY))
                {
                    continue;
                }

                pushX += nodePushX;
                pushY += nodePushY;
            }

            ApplyClampedPush(person, pushX, pushY, maxPushPerTick);
        }
    }

    private static void ApplyClampedPush(Person person, double pushX, double pushY, float maxPushPerTick)
    {
        var magnitude = Math.Sqrt((pushX * pushX) + (pushY * pushY));
        if (magnitude <= 0.0)
        {
            return;
        }

        if (magnitude > maxPushPerTick)
        {
            var scale = maxPushPerTick / magnitude;
            pushX *= scale;
            pushY *= scale;
        }

        person.Position = new Position(person.Position.X + pushX, person.Position.Y + pushY);
    }

    // A positive result moves `a` away from `b` by (pushX, pushY) - `b` moves by the negation
    // of it, wherever the caller wants that applied. False (no push) once they're already far
    // enough apart. Exactly-coincident positions (distance zero, division would be undefined)
    // fall back to a fixed direction rather than leaving two things permanently stuck together.
    private static bool TrySeparation(Position a, Position b, float minDistance, out double pushX, out double pushY)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        if (distance >= minDistance)
        {
            pushX = 0;
            pushY = 0;
            return false;
        }

        var overlap = minDistance - distance;
        if (distance < 0.0001)
        {
            pushX = overlap;
            pushY = 0;
            return true;
        }

        pushX = dx / distance * overlap;
        pushY = dy / distance * overlap;
        return true;
    }

    private void RefreshExploration() =>
        Exploration.Update(_people.Where(p => p.IsAlive).Select(p => p.Position));

    internal void RestorePerson(Person person) => _people.Add(person);

    internal void RestoreForebear(Person forebear) => _forebears.Add(forebear);

    internal void SetNextPersonId(int value) => _nextPersonId = value;

    internal void RestoreResourceNode(ResourceNode node) => _resourceNodes.Add(node);

    internal void SetNextResourceNodeId(int value) => _nextResourceNodeId = value;

    internal void RestoreBuilding(Building building) => _buildings.Add(building);

    internal void SetNextBuildingId(int value) => _nextBuildingId = value;

    internal void RestoreGrave(Grave grave) => _graves.Add(grave);

    internal void SetNextGraveId(int value) => _nextGraveId = value;
}
