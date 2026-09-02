using ManyWinters.Core.Commands;
using ManyWinters.Core.Construction;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.Time;

namespace ManyWinters.Core.World;

public sealed class WorldState
{
    public const long TicksPerYear = TicksPerSeason * SeasonsPerYear;
    public const float MaxInteractionDistance = 2f;

    private const long TicksPerSeason = 75;
    private const long SeasonsPerYear = 4;
    private const float HungerPerTick = 1f;
    private const float MaxHunger = 100f;
    private const float ConditionDecayPerTick = 0.05f;
    private const float MinCondition = 0f;
    private const long MaxLifespanYears = 10;

    private readonly List<Person> _people = new();
    private readonly List<ResourceNode> _resourceNodes = new();
    private readonly List<Building> _buildings = new();
    private readonly List<Grave> _graves = new();
    private int _nextPersonId = 1;
    private int _nextResourceNodeId = 1;
    private int _nextBuildingId = 1;
    private int _nextGraveId = 1;

    public WorldState()
        : this(WorldConfiguration.Empty)
    {
    }

    public WorldState(WorldConfiguration configuration)
    {
        ResourceCatalog = configuration.ResourceCatalog;
        SkillCatalog = configuration.SkillCatalog;
        RecipeCatalog = configuration.RecipeCatalog;
        BuildingCatalog = configuration.BuildingCatalog;
        ItemCatalog = configuration.ItemCatalog;
        SeasonParameters = configuration.SeasonParameters;
    }

    public SimulationClock Clock { get; } = new();

    public ExplorationState Exploration { get; } = new();

    public ResourceCatalog ResourceCatalog { get; }

    public SkillCatalog SkillCatalog { get; }

    public RecipeCatalog RecipeCatalog { get; }

    public BuildingCatalog BuildingCatalog { get; }

    public ItemCatalog ItemCatalog { get; }

    public SeasonParameters SeasonParameters { get; }

    public IReadOnlyList<Person> People => _people;

    public IReadOnlyList<ResourceNode> ResourceNodes => _resourceNodes;

    public IReadOnlyList<Building> Buildings => _buildings;

    public IReadOnlyList<Grave> Graves => _graves;

    public int NextPersonId => _nextPersonId;

    public int NextResourceNodeId => _nextResourceNodeId;

    public int NextBuildingId => _nextBuildingId;

    public int NextGraveId => _nextGraveId;

    public Season CurrentSeason => SeasonAt(Clock.CurrentTick);

    public event Action<Person>? PersonAdded;

    public event Action<ResourceNode>? ResourceNodeAdded;

    public event Action<Building>? BuildingAdded;

    public event Action<Grave>? GraveAdded;

    public Person AddPerson(
        string name,
        Position position,
        long initialAgeTicks = 0,
        PersonId? motherId = null,
        PersonId? fatherId = null)
    {
        var person = new Person
        {
            Id = new PersonId(_nextPersonId++),
            Name = name,
            Position = position,
            BirthTick = Clock.CurrentTick - initialAgeTicks,
            MotherId = motherId,
            FatherId = fatherId,
        };

        _people.Add(person);
        PersonAdded?.Invoke(person);
        RefreshExploration();
        return person;
    }

    public ResourceNode AddResourceNode(ResourceKindId kind, Position position, float amount)
    {
        var node = new ResourceNode
        {
            Id = new ResourceNodeId(_nextResourceNodeId++),
            Kind = kind,
            Position = position,
            RemainingAmount = amount,
            MaxAmount = amount,
        };

        _resourceNodes.Add(node);
        ResourceNodeAdded?.Invoke(node);
        return node;
    }

    public Building AddBuilding(BuildingKindId kind, Position position)
    {
        var building = new Building
        {
            Id = new BuildingId(_nextBuildingId++),
            Kind = kind,
            Position = position,
        };

        _buildings.Add(building);
        BuildingAdded?.Invoke(building);
        return building;
    }

    public Grave AddGrave(
        Position position,
        bool isMarked,
        string? name,
        int? ageAtDeath,
        DeathCause? causeOfDeath,
        string? motherName,
        string? fatherName,
        IReadOnlyList<TechniqueId> knownTechniques)
    {
        var grave = new Grave
        {
            Id = new GraveId(_nextGraveId++),
            Position = position,
            IsMarked = isMarked,
            Name = name,
            AgeAtDeath = ageAtDeath,
            CauseOfDeath = causeOfDeath,
            MotherName = motherName,
            FatherName = fatherName,
            KnownTechniques = knownTechniques,
        };

        _graves.Add(grave);
        GraveAdded?.Invoke(grave);
        return grave;
    }

    public void Execute(ICommand command) => command.Execute(this);

    public static double Distance(Position a, Position b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    public long AgeInYears(Person person) => (Clock.CurrentTick - person.BirthTick) / TicksPerYear;

    public long AgeInSeasons(Person person) => (Clock.CurrentTick - person.BirthTick) / TicksPerSeason;

    // How much this specific person can carry right now - varies by age (see CarryCapacity)
    // plus whatever gear (a basket, a bag, ...) they currently have on them (same "presence,
    // not count" convention as InsulationFor - carrying five baskets isn't five times the
    // bonus of carrying one).
    public float MaxCarryWeightFor(Person person)
    {
        var baseWeight = CarryCapacity.BaseWeightFor(AgeInYears(person), MaxLifespanYears);
        var gearBonus = person.Inventory.Counts.Keys.Sum(ItemCatalog.CarryCapacityBonusFor);
        return baseWeight + gearBonus;
    }

    public void Advance(long ticks)
    {
        var startTick = Clock.CurrentTick;
        Clock.Advance(ticks);

        for (var i = 0L; i < ticks; i++)
        {
            var currentTick = startTick + i + 1;
            var climate = SeasonParameters.ClimateFor(SeasonAt(startTick + i));
            var baseHungerMultiplier = SeasonParameters.HungerMultiplierFor(climate);
            var regenMultiplier = SeasonParameters.RegenMultiplierFor(climate);

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
                    new GatherCommand(person.Id, activeGather.TargetNodeId).Execute(this);
                }

                var insulation = person.Inventory.Counts.Keys.Sum(kind => ItemCatalog.InsulationFor(kind));
                var hungerMultiplier = Math.Max(1f, baseHungerMultiplier - insulation);
                person.Needs.Hunger = Math.Min(person.Needs.Hunger + (HungerPerTick * hungerMultiplier), MaxHunger);
                TryAutoEat(person);

                var age = (currentTick - person.BirthTick) / TicksPerYear;
                var diedOfOldAge = age >= MaxLifespanYears;
                if (person.Needs.Hunger >= MaxHunger || diedOfOldAge)
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

                var definition = ResourceCatalog.Get(node.Kind);
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
            building.Condition = Math.Max(MinCondition, building.Condition - (ConditionDecayPerTick * ticks));
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
        GatherTask gather => !IsWorthGathering(gather.TargetNodeId) || NeedsToSeekFoodUrgently(person),
        _ => false,
    };

    private bool NeedsToSeekFoodUrgently(Person person) =>
        SkillCatalog.Find(EatCommand.Skill) is { } eating
        && person.Needs.Hunger >= HungerSeekFoodThreshold
        && person.KnownTechniques.Contains(eating.BaseTechnique)
        && !HasEdibleFood(person);

    private bool IsWorthGathering(ResourceNodeId nodeId)
    {
        var node = _resourceNodes.FirstOrDefault(n => n.Id == nodeId);
        return node is { IsAlive: true, RemainingAmount: > 0f };
    }

    // "Idle" now means "put whatever skill this person already has to use, or go find food if
    // hungry and empty-handed" (todo: "Pokud už má osoba v idle nějaký skill, tak by ho měl
    // použít") - plain wandering (IdleTask) is only the fallback once neither applies. Hunger
    // takes priority over an already-known skill: a hungry woodcutter with no food on hand
    // goes looking for something to eat before going back to chopping wood.
    private const float HungerSeekFoodThreshold = 50f;

    // Nobody autonomously treks halfway across a real ~1km terrain patch (see
    // MapLoader.ScatterDecorations) for one distant resource - a search this wide only ever
    // matters in a sparse/test world; the real game's decoration density means a genuinely
    // reachable match is normally well within it anyway.
    private const float IdleSearchRadius = 60f;

    private PersonTask DecideIdleTask(Person person)
    {
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
                return new GatherTask(foodNode.Id, foodNode.Position);
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
            return new GatherTask(node.Id, node.Position);
        }

        return new IdleTask();
    }

    private bool IsKnownSkill(Person person, SkillTypeId skill)
    {
        var definition = SkillCatalog.Find(skill);
        return definition is not null && person.KnownTechniques.Contains(definition.BaseTechnique);
    }

    private bool IsFoodResource(ResourceDefinition definition) =>
        definition.YieldsItem is { } item && ItemCatalog.HungerRestoredPerUnitFor(item) > 0f;

    private bool HasEdibleFood(Person person) =>
        person.Inventory.Counts.Any(kv => kv.Value > 0 && ItemCatalog.HungerRestoredPerUnitFor(kv.Key) > 0f);

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
            if (node is not { IsAlive: true, RemainingAmount: > 0f } || !matches(ResourceCatalog.Get(node.Kind)))
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

        return nearestDistance <= IdleSearchRadius ? nearest : null;
    }

    // A lesson someone actually sat down to give (TeachFromSelectedPersonTo's right-click, a
    // deliberate full transfer) is a different thing from picking something up just from being
    // around someone - "tichá pošta": only ever the base technique, never the harder-earned
    // efficient one riding on top of it, and even that isn't guaranteed on any given tick
    // (rolled fresh each tick, not a permanent per-pair verdict - once someone picks something
    // up they can just as easily become a further relay for it, so a low per-tick chance,
    // not a one-time coin flip, is what actually keeps the spread gradual and partial).
    private const float CasualTeachingChancePerTick = 0.05f;

    // Eating (and teaching itself, the one thing every other casual lesson depends on - see
    // AutoTeachNearbyPeople) are different from a specialised craft skill: everyone's watched
    // someone else eat and copy it comes far more naturally than picking up woodcutting from
    // proximity alone, and the whole casual-teaching chain can't even start in a population
    // until at least one person knows how to teach at all. A much higher chance for these two
    // specifically keeps that bootstrap from being the bottleneck it would otherwise be.
    private const float CasualTeachingChancePerTickForCriticalSkills = 0.3f;

    // "Later they teach each other" - once at least one person knows something (and knows how
    // to teach - see TeachCommand), anyone else nearby who doesn't know it yet may pick some of
    // it up automatically, no player action needed. Every alive pair is checked every tick -
    // with the population sizes this game actually has (tens, not thousands, of people), an
    // O(n^2) pass here is negligible next to the resource-node work Advance already does
    // elsewhere.
    private void AutoTeachNearbyPeople(long currentTick)
    {
        // Find, not Get - a catalog that never registered "teaching" (most unit tests, a
        // deliberately minimal world) just means nobody could possibly teach anyone anything,
        // not a crash.
        if (SkillCatalog.Find(TeachCommand.TeachingSkill) is not { } teachingDefinition)
        {
            return;
        }

        var teachingBaseTechnique = teachingDefinition.BaseTechnique;
        var efficientTechniques = SkillCatalog.Definitions.Select(d => d.EfficientTechnique).ToHashSet();
        var criticalTechniques = new HashSet<TechniqueId> { teachingBaseTechnique };
        if (SkillCatalog.Find(EatCommand.Skill) is { } eatingDefinition)
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
                    var chance = criticalTechniques.Contains(technique) ? CasualTeachingChancePerTickForCriticalSkills : CasualTeachingChancePerTick;
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
                    new TeachCommand(teacher.Id, student.Id, techniqueToTeach).Execute(this);
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

            new EatCommand(person.Id, kind).Execute(this);
        }
    }

    // A person's own footprint half-width for collision purposes - deliberately smaller than
    // PersonView's rendered sprite, this only needs to keep people from visibly overlapping,
    // not match their exact silhouette.
    private const float PersonCollisionRadius = 0.35f;

    // Caps how far a single tick's worth of untangling can shove someone, regardless of how
    // many things they happen to be overlapping at once (a person standing in a dense thicket
    // could otherwise be touching several trees' trunks simultaneously, and summing every one
    // of those separations unclamped could shove them noticeably farther in one tick than
    // their own MoveTask/IdleTask step - reading as the person's walk order having been
    // silently hijacked toward some unrelated direction rather than a gentle nudge out of the
    // way). Same order of magnitude as MoveCommand's own walking speed, so being untangled
    // never outpaces an intentional step; a person still deeply stuck simply takes a couple of
    // extra ticks to fully clear, spread out rather than dumped in one lurch.
    private const float MaxCollisionPushPerTick = 1f;

    // MoveTask/IdleTask only ever aim at a destination, with no awareness of who/what else is
    // already there, so this untangles whatever overlap that produced after the fact, every
    // tick - same O(n^2)-over-people precedent as AutoTeachNearbyPeople. Every separation this
    // tick is computed against positions as they stood at the start of it (not updated
    // mid-pass) and summed into one push per person, only applied - clamped - at the end, so
    // the order overlaps happen to be discovered in can't itself bias the result.
    private void ResolveCollisions()
    {
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
                if (!b.IsAlive || !TrySeparation(a.Position, b.Position, PersonCollisionRadius * 2f, out var pushX, out var pushY))
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
                var collisionRadius = ResourceCatalog.Get(node.Kind).CollisionRadius;
                if (!node.IsAlive
                    || collisionRadius <= 0f
                    || !TrySeparation(person.Position, node.Position, PersonCollisionRadius + collisionRadius, out var nodePushX, out var nodePushY))
                {
                    continue;
                }

                pushX += nodePushX;
                pushY += nodePushY;
            }

            ApplyClampedPush(person, pushX, pushY);
        }
    }

    private static void ApplyClampedPush(Person person, double pushX, double pushY)
    {
        var magnitude = Math.Sqrt((pushX * pushX) + (pushY * pushY));
        if (magnitude <= 0.0)
        {
            return;
        }

        if (magnitude > MaxCollisionPushPerTick)
        {
            var scale = MaxCollisionPushPerTick / magnitude;
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

    private static Season SeasonAt(long tick) => (Season)((tick / TicksPerSeason) % SeasonsPerYear);

    private void RefreshExploration() =>
        Exploration.Update(_people.Where(p => p.IsAlive).Select(p => p.Position));

    internal void RestorePerson(Person person) => _people.Add(person);

    internal void SetNextPersonId(int value) => _nextPersonId = value;

    internal void RestoreResourceNode(ResourceNode node) => _resourceNodes.Add(node);

    internal void SetNextResourceNodeId(int value) => _nextResourceNodeId = value;

    internal void RestoreBuilding(Building building) => _buildings.Add(building);

    internal void SetNextBuildingId(int value) => _nextBuildingId = value;

    internal void RestoreGrave(Grave grave) => _graves.Add(grave);

    internal void SetNextGraveId(int value) => _nextGraveId = value;
}
