using ManyWinters.Core.Commands;
using ManyWinters.Core.Construction;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.Population.Naming;
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

    public SimulationClock Clock { get; } = new();

    public ExplorationState Exploration { get; } = new();

    // What any two people mean to each other. A record about pairs, not a list of things in the
    // world, so it has no Add* and announces nothing - the inspector reads it when it draws.
    public Affections Affections { get; } = new();

    // Catalogs, calendar and tuning numbers - fixed for the world's lifetime and not part of a
    // save file.
    public WorldConfiguration Configuration { get; } = configuration;

    public IReadOnlyList<Person> People => _people;

    // People who died before the story began and exist only to be somebody's parent (see
    // Person.Mother): full Person objects a grave or a save file can refer to, but never in
    // People - nothing simulates, draws, counts or clicks them.
    public IReadOnlyList<Person> Forebears => _forebears;

    public IReadOnlyList<ResourceNode> ResourceNodes => _resourceNodes;

    public IReadOnlyList<Building> Buildings => _buildings;

    public IReadOnlyList<Grave> Graves => _graves;

    public Season CurrentSeason => Configuration.Rules.SeasonAt(Clock.CurrentTick);

    public event Action<Person>? PersonAdded;

    public event Action<ResourceNode>? ResourceNodeAdded;

    public event Action<Building>? BuildingAdded;

    public event Action<Grave>? GraveAdded;

    // Add* take a finished object: what it is made of is the caller's business
    // (SpawnPersonCommand, BuryCommand, ...), the world only keeps the list and tells the
    // presentation layer. Ids are drawn by the entity itself (see EntityId).
    public void AddPerson(Person person)
    {
        _people.Add(person);
        PersonAdded?.Invoke(person);
        RefreshExploration();
    }

    // No PersonAdded and no exploration refresh: a forebear is not on the map (see Forebears).
    // A living one would be a person hidden from the simulation, hence the guard.
    public void AddForebear(Person forebear)
    {
        if (forebear.IsAlive)
        {
            throw new ArgumentException("A forebear died before the story began - a living person belongs in People.", nameof(forebear));
        }

        _forebears.Add(forebear);
    }

    public void AddResourceNode(ResourceNode node)
    {
        _resourceNodes.Add(node);
        ResourceNodeAdded?.Invoke(node);
    }

    public void AddBuilding(Building building)
    {
        _buildings.Add(building);
        BuildingAdded?.Invoke(building);
    }

    public void AddGrave(Grave grave)
    {
        _graves.Add(grave);
        GraveAdded?.Invoke(grave);
    }

    public void Execute(ICommand command) => command.Execute(this);

    public static double Distance(Position a, Position b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    // The one proximity test every "act on that thing" command shares; exactly at the limit still
    // counts. `rangeMultiplier` serves the rare wider reach (TeachCommand's efficient teacher).
    public bool IsWithinReach(Position a, Position b, float rangeMultiplier = 1f) =>
        Distance(a, b) <= Configuration.Rules.MaxInteractionDistance * rangeMultiplier;

    // The one "hungry enough to bother" test, shared by TryAutoEat and GatherCommand's eating at
    // the source. The player's Eat button deliberately bypasses it: being told to eat is not the
    // same as deciding to.
    public bool IsHungryEnoughToEat(Person person) => person.Needs.Hunger >= Configuration.Rules.HungerEatThreshold;

    public long AgeInYears(Person person) => AgeInYearsAt(person, Clock.CurrentTick);

    // Age as of some other moment than now - a death tick, say (see BuryCommand).
    public long AgeInYearsAt(Person person, long tick) => (tick - person.BirthTick) / Configuration.Rules.TicksPerYear;

    public long AgeInSeasons(Person person) => (Clock.CurrentTick - person.BirthTick) / Configuration.Rules.TicksPerSeason;

    public LifeStage LifeStageOf(Person person) => LifeStages.For(AgeInYears(person));

    // Grown enough to have children (BirthCommand). Elders count: this is a floor on childhood,
    // not a fertility model - a world whose last two people are old is a story worth telling.
    public bool IsOldEnoughForChildren(Person person) => AgeInYears(person) >= LifeStages.AdultAgeYears;

    // The infant this person is nursing, if any: her own living child, under weaning age and
    // within reach. A scan of People per person per tick is fine at tens of people.
    public Person? NursingInfantOf(Person mother)
    {
        foreach (var person in _people)
        {
            if (IsNursedBy(person, mother))
            {
                return person;
            }
        }

        return null;
    }

    // Reads the same facts as NursingInfantOf independently rather than being told by it, so
    // which of the pair Advance reaches first within a tick cannot change what either gets.
    public bool IsBeingNursed(Person person) => IsNursedBy(person, person.Mother);

    // Age-based base (see CarryCapacity) plus gear bonuses. Presence, not count, as with
    // InsulationFor: five baskets are not five times the bonus of one.
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
                // An empty queue means "use a known skill, or seek food if hungry and
                // empty-handed", falling back to wandering (see DecideIdleTask). Only the
                // autonomous choices are ever revisited, never a player-issued task.
                // IdleGraceUntilTick (GrantIdleGraceCommand) buys a few ticks of standing still,
                // but never past urgent hunger: the grace is renewed every tick while a person
                // is selected, so a hungry one would otherwise never set off for food.
                var idleGraceHolds = currentTick < person.IdleGraceUntilTick && !NeedsToSeekFoodUrgently(person);
                if (!idleGraceHolds && ShouldReconsiderIdleTask(person))
                {
                    var decidedTask = DecideIdleTask(person);
                    if (!KeepsCurrentTask(person.Tasks.Current, decidedTask))
                    {
                        person.Tasks.Interrupt(decidedTask);
                    }
                }

                // Every tick a gather order is active, not once on arrival: GatherTask only
                // walks, the harvest happens here, and GatherCommand no-ops while out of reach.
                if (person.Tasks.Current is GatherTask activeGather)
                {
                    new GatherCommand(person, activeGather.Target).Execute(this);
                }

                // An infant at its mother's side is fed and not hungry; the cost lands on her
                // as NursingHungerMultiplier below. Once she dies or leaves it behind, the
                // countdown is real.
                if (IsBeingNursed(person))
                {
                    person.Needs.Hunger = 0f;
                }
                else
                {
                    var insulation = person.Inventory.Counts.Keys.Sum(kind => itemCatalog.InsulationFor(kind));
                    var hungerMultiplier = Math.Max(1f, baseHungerMultiplier - insulation);
                    if (NursingInfantOf(person) is not null)
                    {
                        hungerMultiplier *= rules.NursingHungerMultiplier;
                    }

                    person.Needs.Hunger = Math.Min(person.Needs.Hunger + (rules.HungerPerTick * hungerMultiplier), person.MaxHunger);
                }

                TryAutoEat(person);

                var diedOfOldAge = AgeInYearsAt(person, currentTick) >= rules.MaxLifespanYears;
                // Their own MaxHunger, not the rules' - see Person.MaxHunger.
                if (person.Needs.Hunger >= person.MaxHunger || diedOfOldAge)
                {
                    person.IsAlive = false;
                    person.DeathTick = currentTick;
                    person.CauseOfDeath = diedOfOldAge ? DeathCause.OldAge : DeathCause.Hunger;
                }
            }

            AutoTeachNearbyPeople(currentTick);
            AdvanceAffections();
            StartFamilies(currentTick);
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

    // Only autonomous tasks are revisited; a player-issued one (MoveTask from MoveCommand) is
    // left alone. IdleTask always gets a second look. GatherTask only once its target stops
    // being worth working (see IsWorthGathering) - re-planning every tick would re-approach the
    // same resource forever - or when hunger becomes urgent (see NeedsToSeekFoodUrgently), so a
    // wood run far from camp can be abandoned for food.
    private bool ShouldReconsiderIdleTask(Person person) => person.Tasks.Current switch
    {
        null => true,
        IdleTask => true,
        // FollowTask never completes, so this is what notices an infant has been weaned.
        FollowTask => true,
        GatherTask gather => !IsWorthGathering(person, gather.Target) || NeedsToSeekFoodUrgently(person),
        _ => false,
    };

    // Whether the freshly decided autonomous task is the one already running, so it is dropped
    // rather than installed. IdleTask carries per-instance state (anchor, leg, pause) that
    // replacing it every tick would throw away; churning a FollowTask for the same mother is
    // pointless. A change of task type always interrupts.
    private static bool KeepsCurrentTask(PersonTask? current, PersonTask decided) => (current, decided) switch
    {
        (IdleTask, IdleTask) => true,
        (FollowTask running, FollowTask fresh) => ReferenceEquals(running.Target, fresh.Target),
        _ => false,
    };

    private bool NeedsToSeekFoodUrgently(Person person) =>
        person.Needs.Hunger >= Configuration.Rules.HungerSeekFoodThreshold
        && KnowsHowToEat(person)
        && !HasEdibleFood(person);

    private bool KnowsHowToEat(Person person) =>
        Configuration.SkillCatalog.Find(EatCommand.Skill) is { } eating && person.KnownTechniques.Contains(eating.BaseTechnique);

    private bool IsWorthGathering(Person person, ResourceNode node) =>
        node is { IsAlive: true, RemainingAmount: > 0f }
        && GatherCommand.CanTakeAnythingFrom(this, person, Configuration.ResourceCatalog.Get(node.Kind), node.RemainingAmount);

    // "Idle" means "use a known skill, or seek food if hungry and empty-handed"; plain wandering
    // (IdleTask) is the fallback. Hunger wins over a known skill (see
    // SimulationRules.HungerSeekFoodThreshold).
    private PersonTask DecideIdleTask(Person person)
    {
        var reachDistance = Configuration.Rules.MaxInteractionDistance;

        // An infant has no skill and nothing to gather, so it keeps up with its mother instead -
        // that is what feeds it (see Advance) and what keeps it within teaching reach. An orphan
        // falls through and wanders like anybody else; nothing here saves it, and nothing should.
        if (LifeStageOf(person) == LifeStage.Infant && person.Mother.IsAlive)
        {
            return new FollowTask(person.Mother, reachDistance, Configuration.Rules.InfantFollowSpeedPerTick);
        }
        // Without knowing how to eat, gathering food would not help (see EatCommand), so this
        // falls through to the general search below.
        if (NeedsToSeekFoodUrgently(person))
        {
            // A food resource this person never learned to gather is as unreachable as none.
            var foodNode = FindNearestGatherableResourceNode(person, definition => IsFoodResource(definition) && IsKnownSkill(person, definition.Skill));
            if (foodNode is not null)
            {
                return new GatherTask(foodNode, reachDistance);
            }
        }

        // Nearest wins regardless of which known skill it needs. IsKnownSkill checks the skill's
        // BaseTechnique, since KnownTechniques holds arbitrary techniques rather than skills.
        var node = FindNearestGatherableResourceNode(person, definition => IsKnownSkill(person, definition.Skill));
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

    private bool IsNursedBy(Person person, Person mother) =>
        person.IsAlive
        && mother.IsAlive
        && ReferenceEquals(person.Mother, mother)
        && LifeStageOf(person) == LifeStage.Infant
        && IsWithinReach(person.Position, mother.Position);

    private bool IsFoodResource(ResourceDefinition definition) =>
        definition.YieldsItem is { } item && Configuration.ItemCatalog.HungerRestoredPerUnitFor(item) > 0f;

    private bool HasEdibleFood(Person person) =>
        person.Inventory.Counts.Any(kv => kv.Value > 0 && Configuration.ItemCatalog.HungerRestoredPerUnitFor(kv.Key) > 0f);

    // Depleted-but-alive nodes (RemainingAmount 0, regenerating) are skipped - a fuller one of
    // the same kind is normally nearby - and so is anything this person could not take from
    // (see GatherCommand.CanTakeAnythingFrom): nobody walks to a source to gather nothing.
    private ResourceNode? FindNearestGatherableResourceNode(Person person, Func<ResourceDefinition, bool> matches)
    {
        ResourceNode? nearest = null;
        var nearestDistance = double.MaxValue;
        foreach (var node in _resourceNodes)
        {
            if (!IsWorthGathering(person, node) || !matches(Configuration.ResourceCatalog.Get(node.Kind)))
            {
                continue;
            }

            var distance = Distance(person.Position, node.Position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = node;
            }
        }

        return nearestDistance <= Configuration.Rules.IdleSearchRadius ? nearest : null;
    }

    // Once somebody knows a technique and how to teach (see TeachCommand), anyone nearby may
    // pick it up without a player action; SimulationRules.CasualTeachingChancePerTick says why
    // it is a per-tick roll. Every living pair every tick: O(n^2) is negligible at tens of people.
    private void AutoTeachNearbyPeople(long currentTick)
    {
        var skillCatalog = Configuration.SkillCatalog;
        var rules = Configuration.Rules;

        // Find, not Get: a catalog without "teaching" (most unit tests) means nobody can teach,
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

    // Time together grows a bond, time apart loses it, for every living pair every tick (O(n^2),
    // negligible at tens of people). Pairs involving the dead are skipped rather than decayed,
    // so what someone meant to others is still there to read after they are gone.
    private void AdvanceAffections()
    {
        var rules = Configuration.Rules;
        for (var i = 0; i < _people.Count; i++)
        {
            var first = _people[i];
            if (!first.IsAlive)
            {
                continue;
            }

            for (var j = i + 1; j < _people.Count; j++)
            {
                var second = _people[j];
                if (!second.IsAlive)
                {
                    continue;
                }

                var together = Distance(first.Position, second.Position) <= rules.TogetherDistance;
                var delta = together ? rules.AffectionGainedPerTickTogether : -rules.AffectionLostPerTickApart;
                Affections.Change(first.Id, second.Id, delta, rules.MaxAffection);
            }
        }
    }

    // Where children come from when nobody asks. Whether a birth is possible is BirthCommand's
    // business; this pass adds only the bond threshold
    // (SimulationRules.AffectionNeededToHaveAChild). Iterates a snapshot because BirthCommand
    // adds to _people: a child must not become a candidate parent on the tick it is born.
    private void StartFamilies(long currentTick)
    {
        var threshold = Configuration.Rules.AffectionNeededToHaveAChild;
        var candidates = _people.Where(person => person.IsAlive && IsOldEnoughForChildren(person)).ToList();

        for (var i = 0; i < candidates.Count; i++)
        {
            for (var j = i + 1; j < candidates.Count; j++)
            {
                var first = candidates[i];
                var second = candidates[j];
                if (Affections.Between(first.Id, second.Id) < threshold)
                {
                    continue;
                }

                var mother = first.Sex == Sex.Female ? first : second;
                var father = ReferenceEquals(mother, first) ? second : first;

                // Alive, grown, one of each sex, not kin, within reach, mother not nursing - all
                // checked inside; it declines silently like any other command.
                new BirthCommand(NameForNewborn(mother, father, currentTick), mother, father).Execute(this);
            }
        }
    }

    // Slow enough that no single generation overwrites the naming tradition it was handed
    // (docs/Procedural Name Generation Plan.md, "Cultural Memory"); the last ~12 births (roughly
    // one generation, SimulationRules.Default) count for the separate NamingTrend on top.
    private const float CultureDecayPerObservation = 0.98f;
    private const int RecentTrendWindow = 12;

    private int _namingHistoryVersion = -1;
    private CultureProfile? _cachedCultureProfile;
    private CultureProfile? _cachedTrendProfile;
    private HashSet<string>? _cachedExistingNames;

    // Deterministic from the parents and the tick, so a replayed world names the same children
    // (SeedHash.Avalanche, as CasualTeachingSeed below). The culture it draws from is rebuilt
    // from People/Forebears rather than saved separately - see NamingProfiles.
    //
    // Public because a child the player asks for is named the same way as one the band has of
    // its own accord (see TargetActions): BirthCommand takes the name, so somebody has to draw
    // it, and there is only one right way to draw it.
    public string NameForNewborn(Person mother, Person father, long tick)
    {
        var (culture, trend, existingNames) = NamingProfiles();
        var siblingNames = _people
            .Where(person => ReferenceEquals(person.Mother, mother) && ReferenceEquals(person.Father, father))
            .Select(person => person.Name)
            .ToList();

        var mixed = unchecked((uint)(mother.Id.Seed * 73856093) ^ (uint)(father.Id.Seed * 19349663) ^ ((uint)tick * 2654435761u));
        var rng = new Random(SeedHash.Avalanche(mixed));

        return PhoneticNameGenerator.GenerateChild(rng, culture, trend, mother.Name, father.Name, existingNames, siblingNames);
    }

    // A name for someone with no parents to inherit from, drawn from the current naming culture
    // rather than a curated pool - what the player's manual "Spawn Person" button uses
    // (Main.OnSpawnButtonPressed).
    public string GenerateUnrelatedName(Random rng)
    {
        var (culture, trend, existingNames) = NamingProfiles();
        return PhoneticNameGenerator.GenerateChild(rng, culture, trend, motherName: null, fatherName: null, existingNames, siblingNames: []);
    }

    // Cached against People.Count + Forebears.Count (both only ever grow): rebuilding the whole
    // profile from history is cheap once, but StartFamilies calls NameForNewborn speculatively
    // for every eligible pair on every tick, and only some of those become an actual birth.
    private (CultureProfile Culture, CultureProfile Trend, HashSet<string> ExistingNames) NamingProfiles()
    {
        var version = _people.Count + _forebears.Count;
        if (version != _namingHistoryVersion || _cachedCultureProfile is null || _cachedTrendProfile is null || _cachedExistingNames is null)
        {
            var history = _forebears.Concat(_people).OrderBy(person => person.BirthTick).Select(person => person.Name).ToList();
            _cachedCultureProfile = CultureProfile.Build(history, CultureDecayPerObservation);
            _cachedTrendProfile = CultureProfile.BuildRecentTrend(history, RecentTrendWindow);
            _cachedExistingNames = new HashSet<string>(history, StringComparer.OrdinalIgnoreCase);
            _namingHistoryVersion = version;
        }

        return (_cachedCultureProfile, _cachedTrendProfile, _cachedExistingNames);
    }

    // Deterministic from the ids' seeds (EntityId.SeedOf) and the tick, as IdleTask.SeedFor is,
    // rather than a shared Random: reproducible and independent of call order between people.
    private static bool PassesCasualTeachingRoll(PersonId teacherId, PersonId studentId, TechniqueId technique, long tick, float chance)
    {
        var seed = CasualTeachingSeed(teacherId.Seed, studentId.Seed, technique.Value, tick);

        // Stryker disable once Equality: NextDouble() returning exactly `chance` has
        // probability zero, so < and <= are the same roll
        return new Random(seed).NextDouble() < chance;
    }

    private static int CasualTeachingSeed(int teacherSeed, int studentSeed, string technique, long tick)
    {
        // Spread by SeedHash so adjacent ids and consecutive ticks do not roll alike.
        var mixed = unchecked((uint)(teacherSeed * 73856093) ^ (uint)(studentSeed * 19349663) ^ (uint)(StableStringHash(technique) * 83492791) ^ ((uint)tick * 2654435761u));

        return SeedHash.Avalanche(mixed);
    }

    // Not string.GetHashCode(): .NET randomizes it per process, and this roll must be stable.
    private static int StableStringHash(string value)
    {
        var hash = 5381;
        foreach (var c in value)
        {
            hash = unchecked((hash * 33) ^ c);
        }

        return hash;
    }

    // Same behaviour as Main's Eat button (OnEatButtonPressed): eats through whatever food is on
    // hand until no longer hungry. Runs every tick whatever task is active, even a player-issued
    // one - a starving person should not wait for a free moment to eat from their own pack.
    private void TryAutoEat(Person person)
    {
        // A meal, not a nibble: nothing until hunger has built up, then EatCommand eats to zero.
        if (!IsHungryEnoughToEat(person))
        {
            return;
        }

        foreach (var kind in person.Inventory.Counts.Keys.ToList())
        {
            // Stryker disable once Equality,Statement,Block: EatCommand no-ops at zero hunger anyway, so this only saves the remaining calls
            if (person.Needs.Hunger <= 0f)
            {
                break;
            }

            new EatCommand(person, kind).Execute(this);
        }
    }

    // MoveTask/IdleTask aim at a destination with no awareness of what else is there, so this
    // untangles the overlap afterwards, every tick (O(n^2), as AutoTeachNearbyPeople).
    // Separations are computed against start-of-tick positions and summed into one clamped push
    // per person (SimulationRules.MaxCollisionPushPerTick), so discovery order cannot bias the
    // result.
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
        // Stryker disable once Equality,Statement,Block: falling through adds a zero push and lands on the same spot
        if (magnitude <= 0.0)
        {
            return;
        }
        // Stryker disable once Equality: at exactly the cap the scale is 1, so clamping changes nothing
        if (magnitude > maxPushPerTick)
        {
            var scale = maxPushPerTick / magnitude;
            pushX *= scale;
            pushY *= scale;
        }

        person.Position = new Position(person.Position.X + pushX, person.Position.Y + pushY);
    }

    // A true result moves `a` away from `b` by (pushX, pushY); `b` gets the negation, wherever
    // the caller applies it. False once far enough apart. Coincident positions fall back to a
    // fixed direction rather than staying stuck together.
    private static bool TrySeparation(Position a, Position b, float minDistance, out double pushX, out double pushY)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        // Stryker disable once Equality: at exactly the minimum the overlap is zero, so either branch stands still
        if (distance >= minDistance)
        {
            pushX = 0;
            pushY = 0;
            // Stryker disable once Boolean: both out parameters are zero here, so either answer leaves every position as it was
            return false;
        }

        var overlap = minDistance - distance;
        // Stryker disable once Equality: two positions exactly this far apart has probability zero
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

    internal void RestoreResourceNode(ResourceNode node) => _resourceNodes.Add(node);

    internal void RestoreBuilding(Building building) => _buildings.Add(building);

    internal void RestoreGrave(Grave grave) => _graves.Add(grave);
}
