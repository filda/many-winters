using ManyWinters.Core.Commands;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
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
    private readonly List<Animal> _animals = new();
    private readonly List<Entity> _entities = new();
    private readonly List<Grave> _graves = new();
    private readonly List<HomeRange> _homeRanges = new();

    public SimulationClock Clock { get; } = new();

    public ExplorationState Exploration { get; } = new();

    // What any two people mean to each other. A record about pairs, not a list of things in the
    // world, so it has no Add* and announces nothing - the inspector reads it when it draws.
    public Affections Affections { get; } = new();

    // The words this band has for the things it makes. Empty at the start: nobody has made
    // anything, so there is nothing to have a word for.
    public Vocabulary Vocabulary { get; } = new();

    // Catalogs, calendar and tuning numbers - fixed for the world's lifetime and not part of a
    // save file.
    public WorldConfiguration Configuration { get; } = configuration;

    public IReadOnlyList<Person> People => _people;

    // People who died before the story began and exist only to be somebody's parent: full
    // Person objects a grave or a save file can refer to, but never in People - nothing
    // simulates, draws, counts or clicks them.
    public IReadOnlyList<Person> Forebears => _forebears;

    // The second kind of Creature: simulated in Advance alongside People, but never a target of
    // the people-only passes (AutoTeachNearbyPeople, AdvanceAffections, StartFamilies, ...).
    public IReadOnlyList<Animal> Animals => _animals;

    public IReadOnlyList<Entity> Entities => _entities;

    public IReadOnlyList<Grave> Graves => _graves;

    // Shared, slowly drifting anchors a herd wanders around. Advanced once per tick in Advance.
    public IReadOnlyList<HomeRange> HomeRanges => _homeRanges;

    public Season CurrentSeason => Configuration.Rules.SeasonAt(Clock.CurrentTick);

    public event Action<Person>? PersonAdded;

    public event Action<Animal>? AnimalAdded;

    public event Action<Entity>? EntityAdded;

    public event Action<Grave>? GraveAdded;

    // Only a pile-category entity fires this today.
    public event Action<Entity>? EntityRemoved;

    // A dead, unburied animal whose bones have finally lingered past
    // SimulationRules.BonesLingerTicks - fired by the decay pass in Advance, mirroring
    // EntityRemoved.
    public event Action<Animal>? AnimalRemoved;

    // Add* take a finished object: what it is made of is the caller's business
    // (SpawnPersonCommand, BuryCommand, ...), the world only keeps the list and tells the
    // presentation layer. Ids are drawn by the entity itself.
    public void AddPerson(Person person)
    {
        _people.Add(person);
        PersonAdded?.Invoke(person);
        RefreshExploration();
    }

    // No PersonAdded and no exploration refresh: a forebear is not on the map. A living one
    // would be a person hidden from the simulation, hence the guard.
    public void AddForebear(Person forebear)
    {
        if (forebear.IsAlive)
        {
            throw new ArgumentException("A forebear died before the story began - a living person belongs in People.", nameof(forebear));
        }

        _forebears.Add(forebear);
    }

    public void AddAnimal(Animal animal)
    {
        _animals.Add(animal);
        AnimalAdded?.Invoke(animal);
    }

    public void AddEntity(Entity entity)
    {
        _entities.Add(entity);
        EntityAdded?.Invoke(entity);
    }

    // No event: a home range is scenery for the simulation, not something the presentation layer
    // draws on its own (unlike a Person, Entity or Grave) - a phase 2 AnimalView reads it off its
    // Animal's own Home instead.
    public void AddHomeRange(HomeRange homeRange) => _homeRanges.Add(homeRange);

    public void AddGrave(Grave grave)
    {
        _graves.Add(grave);
        GraveAdded?.Invoke(grave);
    }

    // Called once a pile's StaticAmount reaches zero: an empty pile has nothing left for anyone
    // to point at. A growable entity that dies is never removed this way - it stays in Entities
    // with Growth.IsAlive false.
    public void RemoveEntity(Entity entity)
    {
        _entities.Remove(entity);
        EntityRemoved?.Invoke(entity);
    }

    // Bones gone into the ground: the corpse's own decay pass (Advance) calls this once its
    // bones have lingered past SimulationRules.BonesLingerTicks. Never called for a Person - see
    // AnimalRemoved.
    private void RemoveAnimal(Animal animal)
    {
        _animals.Remove(animal);
        AnimalRemoved?.Invoke(animal);
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
    public bool IsHungryEnoughToEat(Creature creature) => creature.Needs.Hunger >= Configuration.Rules.HungerEatThreshold;

    public long AgeInYears(Creature creature) => AgeInYearsAt(creature, Clock.CurrentTick);

    // Age as of some other moment than now - a death tick, say.
    public long AgeInYearsAt(Creature creature, long tick) => (tick - creature.BirthTick) / Configuration.Rules.TicksPerYear;

    public long AgeInSeasons(Person person) => (Clock.CurrentTick - person.BirthTick) / Configuration.Rules.TicksPerSeason;

    public LifeStage LifeStageOf(Creature creature) => LifeCycleOf(creature).StageFor(AgeInYears(creature));

    // Grown enough to have children. Elders count: this is a floor on childhood, not a fertility
    // model - a world whose last two people are old is a story worth telling.
    public bool IsOldEnoughForChildren(Person person) => AgeInYears(person) >= LifeCycleOf(person).AdultAgeYears;

    // A creature's own species' age bands and lifespan. A dictionary lookup per call is fine at
    // tens of people per tick; nothing here caches it.
    private LifeCycle LifeCycleOf(Creature creature) => Configuration.SpeciesCatalog.Get(creature.Species).LifeCycle;

    // The only place that answers "how much hunger does this item put right for this creature":
    // nutrition is the item's own, digestibility is the species' - a wolf can eat a pear but it
    // will not keep it going, and grass feeds a deer and not a person. Every reader with a
    // creature in hand goes through this rather than the catalog directly.
    public float HungerRestoredPerUnitFor(Creature creature, ItemKindId item)
    {
        var itemCatalog = Configuration.ItemCatalog;
        var nutrition = itemCatalog.HungerRestoredPerUnitFor(item);
        // An item nobody described (or one that puts nothing right anyway) is skipped before
        // Get, the same "undescribed is weightless" pattern ItemCatalog's own readers use -
        // never a KeyNotFoundException for something that was never going to matter.
        if (nutrition <= 0f)
        {
            return 0f;
        }

        var species = Configuration.SpeciesCatalog.Get(creature.Species);
        return nutrition * species.DigestibilityOf(itemCatalog.Get(item).Material);
    }

    // The infant this creature is nursing, if any: its own living child, under weaning age and
    // within reach. A scan of every living creature per creature per tick is fine at tens of them.
    public Creature? NursingInfantOf(Creature mother)
    {
        foreach (var creature in AllCreatures())
        {
            if (IsNursedBy(creature, mother))
            {
                return creature;
            }
        }

        return null;
    }

    // Reads the same facts as NursingInfantOf independently rather than being told by it, so
    // which of the pair Advance reaches first within a tick cannot change what either gets.
    public bool IsBeingNursed(Creature creature) => IsNursedBy(creature, creature.NursingMother);

    // Age-based base plus gear bonuses. Presence, not count, as with InsulationFor: five baskets
    // are not five times the bonus of one. 0 outright for a species that can't carry anything at
    // all - an animal's CanCarry is false, so it never pockets what it grazes.
    public float MaxCarryWeightFor(Creature creature)
    {
        return CarryCapacity.MaxCarryWeightFor(creature, Configuration, AgeInYears(creature), LifeCycleOf(creature));
    }

    // Every living Person then every living Animal, for the per-creature passes in Advance and
    // for anything (NursingInfantOf, ResolveCollisions) that has to look across both.
    private IEnumerable<Creature> AllCreatures() => _people.Cast<Creature>().Concat(_animals);

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

            foreach (var creature in AllCreatures())
            {
                if (!creature.IsAlive)
                {
                    continue;
                }

                creature.Tasks.Advance(creature);
                // An empty queue means "use a known skill, or seek food if hungry and
                // empty-handed", falling back to wandering. IdleGraceUntilTick buys a few ticks
                // of standing still, but never past urgent hunger: the grace is renewed every
                // tick while a person is selected, so a hungry one would otherwise never set
                // off for food.
                var idleGraceHolds = currentTick < creature.IdleGraceUntilTick && !NeedsToSeekFoodUrgently(creature);
                if (!idleGraceHolds && ShouldReconsiderIdleTask(creature))
                {
                    var decidedTask = DecideIdleTask(creature);
                    if (!KeepsCurrentTask(creature.Tasks.Current, decidedTask))
                    {
                        creature.Tasks.Interrupt(decidedTask);
                    }
                }

                // Every tick a gather order is active, not once on arrival: GatherTask only
                // walks, the harvest happens here, and both commands no-op while out of reach.
                if (creature.Tasks.Current is GatherTask activeGather)
                {
                    ICommand take = activeGather.Target.Category == EntityCategory.Pile
                        ? new EatFromPileCommand(creature, activeGather.Target)
                        : new GatherCommand(creature, activeGather.Target);
                    take.Execute(this);
                }

                // A throw is only attempted once in range, and costs time like a workbench
                // attempt - HuntTask only ever walks. Only a Person ever hunts (nothing grants
                // basic_hunting to an animal), so the type check here is the same guard
                // ButcherCommand relies on.
                if (creature.Tasks.Current is HuntTask activeHunt
                    && creature is Person hunter
                    && Distance(creature.Position, activeHunt.Prey.Position) <= activeHunt.Range
                    && currentTick >= activeHunt.NextAttemptTick)
                {
                    new HuntCommand(hunter, activeHunt.Prey).Execute(this);
                    activeHunt.NextAttemptTick = currentTick + rules.TicksPerWorkAttempt;
                }

                // Every tick a butcher order is active, not once on arrival - exactly the
                // GatherTask/pile pattern above, and ButcherCommand's own Blocker no-ops while
                // still out of reach.
                if (creature.Tasks.Current is ButcherTask activeButcher && creature is Person butcher)
                {
                    new ButcherCommand(butcher, activeButcher.Carcass).Execute(this);
                }

                // An infant at its mother's side is fed and not hungry; the cost lands on her
                // as NursingHungerMultiplier below. Once she dies or leaves it behind, the
                // countdown is real.
                if (IsBeingNursed(creature))
                {
                    creature.Needs.Hunger = 0f;
                }
                else
                {
                    var insulation = creature.Inventory.Counts.Keys.Sum(kind => itemCatalog.InsulationFor(kind));
                    var hungerMultiplier = Math.Max(1f, baseHungerMultiplier - insulation);
                    if (NursingInfantOf(creature) is not null)
                    {
                        hungerMultiplier *= rules.NursingHungerMultiplier;
                    }

                    // The species' own winter reserve (SpeciesDefinition.HungerPerTickMultiplier) -
                    // 1 for a human, so this changes nothing about a person.
                    var speciesHungerMultiplier = Configuration.SpeciesCatalog.Get(creature.Species).HungerPerTickMultiplier;

                    creature.Needs.Hunger = Math.Min(creature.Needs.Hunger + (rules.HungerPerTick * hungerMultiplier * speciesHungerMultiplier), creature.MaxHunger);
                }

                TryAutoEat(creature);

                var diedOfOldAge = AgeInYearsAt(creature, currentTick) >= LifeCycleOf(creature).MaxLifespanYears;
                // Their own MaxHunger, not the rules'.
                if (creature.Needs.Hunger >= creature.MaxHunger || diedOfOldAge)
                {
                    creature.IsAlive = false;
                    creature.DeathTick = currentTick;
                    creature.CauseOfDeath = diedOfOldAge ? DeathCause.OldAge : DeathCause.Hunger;
                    FillCarcass(creature);
                }
            }

            // A year further on, a dead animal's bones themselves are gone - a person's never
            // are. Snapshotted: RemoveAnimal mutates _animals mid-iteration otherwise.
            foreach (var animal in _animals.ToList())
            {
                if (animal.IsAlive || animal.DeathTick is not { } deathTick)
                {
                    continue;
                }

                if (currentTick - deathTick == rules.CorpseDecayTicks + rules.BonesLingerTicks)
                {
                    RemoveAnimal(animal);
                }
            }

            // Spoilage: every stack and worked object, in every creature's pack (alive or dead -
            // a corpse's meat rots on its own clock, not at CorpseDecayTicks), in every
            // building's storage, and on every ground pile or Made thing lying loose. What a
            // resource node itself holds never spoils - it grows.
            foreach (var creature in AllCreatures())
            {
                creature.Inventory.Expire(currentTick, itemCatalog);
            }

            foreach (var entity in _entities.ToList())
            {
                entity.Storage?.Expire(currentTick, itemCatalog);

                if (entity.Made is { } made
                    && itemCatalog.ShelfLifeTicksOf(made) is { } madeShelfLife
                    && currentTick - made.MadeTick >= madeShelfLife)
                {
                    RemoveEntity(entity);
                    continue;
                }

                if (entity is { Category: EntityCategory.Pile, Made: null, StaticAmount: > 0, DroppedTick: { } droppedTick }
                    && itemCatalog.ShelfLifeFor(new ItemKindId(entity.Kind.Value)) is { } pileShelfLife
                    && currentTick - droppedTick >= pileShelfLife)
                {
                    entity.StaticAmount = 0;
                    RemoveEntity(entity);
                }
            }

            AutoTeachNearbyPeople(currentTick);
            LearnWhatIsInHand();
            ShareWhatTheyKnow(currentTick);
            DiscoverByFiddling(currentTick);
            AdvanceAffections();
            StartFamilies(currentTick);
            BreedAnimals(currentTick, climate);
            ResolveCollisions();
            RefreshExploration();

            foreach (var homeRange in _homeRanges)
            {
                homeRange.Advance(currentTick, rules.TicksPerSeason);
            }

            foreach (var entity in _entities)
            {
                if (entity.Growth is not { IsAlive: true } growth)
                {
                    continue;
                }

                var definition = resourceCatalog.Get(entity.Kind);
                if (definition.IsInhospitable(climate))
                {
                    growth.ColdStress += 1f;
                    if (growth.ColdStress >= definition.TicksToWither)
                    {
                        growth.IsAlive = false;
                        growth.DeathTick = currentTick;
                        growth.CauseOfDeath = ResourceDeathCause.Climate;
                    }

                    continue;
                }

                growth.ColdStress = 0f;

                var regenPerTick = definition.RegenPerTick * regenMultiplier;
                growth.RemainingAmount = Math.Min(growth.MaxAmount, growth.RemainingAmount + regenPerTick);
            }
        }

        foreach (var entity in _entities)
        {
            if (entity.Condition is not { } condition)
            {
                continue;
            }

            entity.Condition = Math.Max(MinCondition, condition - (rules.ConditionDecayPerTick * ticks));
        }
    }

    // Only autonomous tasks are revisited; a player-issued one (MoveTask from MoveCommand) is
    // left alone. IdleTask always gets a second look. GatherTask only once its target stops
    // being worth working - re-planning every tick would re-approach the same resource forever -
    // or when hunger becomes urgent, so a wood run far from camp can be abandoned for food.
    private bool ShouldReconsiderIdleTask(Creature creature) => creature.Tasks.Current switch
    {
        null => true,
        IdleTask => true,
        // FollowTask never completes, so this is what notices an infant has been weaned.
        FollowTask => true,
        // Reconsidered every tick like IdleTask/FollowTask, but the flee check below hands back
        // the very same FleeTask while it is still running rather than re-deriving completion
        // from FleeDistance, which would cut the flee short the moment the gap merely passes
        // FleeDistance on the way out to the wider SafeDistance.
        FleeTask => true,
        // A threat closing in is worth dropping a gather order for, same as urgent hunger.
        GatherTask gather => !IsWorthTakingFrom(creature, gather.Target) || NeedsToSeekFoodUrgently(creature) || NearbyThreatTo(creature) is not null,
        // The prey died (to this hunter or anyone else), wandered out of the search radius, or
        // hunger is (still) urgent - mirroring GatherTask's own NeedsToSeekFoodUrgently branch
        // above, which re-derives the best option every tick while hungry rather than committing
        // to one target. Deliberately still HungerSeekFoodThreshold here, not WouldEatIfTheyCould's
        // lower one: re-deriving from the lower threshold thrashes rather than helps, since a
        // hunter closing on one deer keeps getting handed whichever different deer is now
        // nearest (the herd scatters as the hunter's own approach spooks it, so "nearest" keeps
        // changing faster than any one chase can finish), which starves them worse than
        // committing to one target between hunger 25 and 50 would. Below 50 a freshly installed
        // hunt is left to run uninterrupted rather than re-picking a target on every tick; above
        // 50 this re-derives as before, and a hungry hunter who has since come to carry food is
        // dropped here rather than hunting on for more.
        HuntTask hunt => !hunt.Prey.IsAlive
            || Distance(creature.Position, hunt.Prey.Position) > Configuration.Rules.IdleSearchRadius
            || creature.Needs.Hunger >= Configuration.Rules.HungerSeekFoodThreshold,
        // No *meat* left, specifically - not "the carcass is totally empty": a carcass a
        // beginner stripped of meat but left hide and sinew on still has a nonzero inventory,
        // and a hungry butcher parked beside it forever would starve next to something that can
        // no longer feed them. No hunger term here either, for the same reason HuntTask above
        // stays off WouldEatIfTheyCould: a carcass does not move, so there is less to gain from
        // re-deriving mid-walk, and nothing to lose by not doing so.
        ButcherTask butcher => butcher.Carcass.Inventory.Get(ButcherCommand.Meat) <= 0,
        _ => false,
    };

    // Whether the freshly decided autonomous task is the one already running, so it is dropped
    // rather than installed. IdleTask carries per-instance state (anchor, leg, pause) that
    // replacing it every tick would throw away; churning a FollowTask for the same mother is
    // pointless. A change of task type always interrupts. FleeTask keeps running against the same
    // threat rather than restarting every tick - restarting would not change anything (Advance
    // recomputes the direction every tick regardless), but it would be needless churn.
    private static bool KeepsCurrentTask(CreatureTask? current, CreatureTask decided) => (current, decided) switch
    {
        (IdleTask, IdleTask) => true,
        (FollowTask running, FollowTask fresh) => ReferenceEquals(running.Target, fresh.Target),
        (FleeTask running, FleeTask fresh) => ReferenceEquals(running.Threat, fresh.Threat),
        (HuntTask running, HuntTask fresh) => ReferenceEquals(running.Prey, fresh.Prey),
        (ButcherTask running, ButcherTask fresh) => ReferenceEquals(running.Carcass, fresh.Carcass),
        _ => false,
    };

    private bool NeedsToSeekFoodUrgently(Creature creature) =>
        creature.Needs.Hunger >= Configuration.Rules.HungerSeekFoodThreshold
        && KnowsHowToEat(creature)
        && !HasEdibleFood(creature);

    // The same "hungry, knows how to eat, empty-handed" test as NeedsToSeekFoodUrgently, at
    // HungerEatThreshold rather than HungerSeekFoodThreshold - "would eat if they had something"
    // rather than "must go find something now". Used only for the two animal food steps below: a
    // hunt is a long trip - closing on prey, then walking to and butchering the carcass - that a
    // person already committed to gathering nearby plant food or a pile would not be worth
    // interrupting for, but is worth setting out on well before hunger becomes urgent, unlike a
    // two-step trip to a nearby tree. Gathering plant food and piles keep
    // NeedsToSeekFoodUrgently's higher threshold unchanged - that pacing is a separate decision
    // the family milestone depends on.
    private bool WouldEatIfTheyCould(Creature creature) =>
        creature.Needs.Hunger >= Configuration.Rules.HungerEatThreshold
        && KnowsHowToEat(creature)
        && !HasEdibleFood(creature);

    private bool KnowsHowToEat(Creature creature) =>
        Configuration.SkillCatalog.Find(EatCommand.Skill) is { } eating && creature.KnownTechniques.Contains(eating.BaseTechnique);

    private bool IsWorthGathering(Creature creature, Entity entity) =>
        entity.Growth is { IsAlive: true, RemainingAmount: > 0f } growth
        && GatherCommand.CanTakeAnythingFrom(this, creature, Configuration.ResourceCatalog.Get(entity.Kind), growth.RemainingAmount);

    // A pile is only ever a GatherTask target for a meal, so once the meal is eaten there is
    // nothing left to stand beside it for.
    private bool IsWorthTakingFrom(Creature creature, Entity entity) =>
        entity.Category == EntityCategory.Pile
            ? IsFoodPile(creature, entity) && IsHungryEnoughToEat(creature)
            : IsWorthGathering(creature, entity);

    // Food for a given creature about to be sent there, not food in general: a pile of the same
    // material that creature's species cannot digest is not worth the walk.
    private bool IsFoodPile(Creature creature, Entity entity) =>
        entity is { Category: EntityCategory.Pile, StaticAmount: > 0 }
        && HungerRestoredPerUnitFor(creature, EatFromPileCommand.FoodOf(entity)) > 0f;

    // "Idle" means "use a known skill, or seek food if hungry and empty-handed"; plain wandering
    // (IdleTask) is the fallback. Hunger wins over a known skill. A search centres on the
    // creature's own HomeRange anchor when it has one (an animal) rather than on where it
    // happens to be standing (a person, today).
    private CreatureTask DecideIdleTask(Creature creature)
    {
        var reachDistance = Configuration.Rules.MaxInteractionDistance;
        var searchOrigin = creature.Home?.Anchor ?? creature.Position;

        // A threat wins over everything else, including an infant's own mother and hunger: a
        // species that never notices a person standing next to it reads as broken. Species data,
        // not a type check on the creature - a human never has one.
        if (Configuration.SpeciesCatalog.Get(creature.Species).Flee is { } flee)
        {
            // Already running from something and not yet clear of it (FleeTask.IsComplete) -
            // hand back the same instance rather than re-deriving from FleeDistance, or a flee
            // already under way would be cut short the moment the gap merely passes FleeDistance
            // on the way out to the wider SafeDistance.
            if (creature.Tasks.Current is FleeTask activeFlee)
            {
                return activeFlee;
            }

            if (NearbyThreatTo(creature) is { } threat)
            {
                return new FleeTask(threat, flee);
            }
        }

        // An infant has no skill and nothing to gather, so it keeps up with its mother instead -
        // that is what feeds it and what keeps it within teaching reach. An orphan falls through
        // and wanders like anybody else; nothing here saves it, and nothing should.
        if (LifeStageOf(creature) == LifeStage.Infant && creature.NursingMother is { IsAlive: true } mother)
        {
            return new FollowTask(mother, reachDistance, Configuration.Rules.InfantFollowSpeedPerTick);
        }
        // Without knowing how to eat, gathering food would not help, so this falls through to
        // the general search below.
        if (NeedsToSeekFoodUrgently(creature))
        {
            // A food resource this creature never learned to gather is as unreachable as none,
            // but food somebody put down needs no skill to take. Nearest wins.
            var foodNode = FindNearestGatherableEntity(creature, searchOrigin, definition => IsFoodResource(creature, definition) && IsKnownSkill(creature, definition.Skill));
            var food = NearerOf(searchOrigin, foodNode, FindNearestFoodPile(creature, searchOrigin));
            if (food is not null)
            {
                // A pile is taken from at the tighter PileReachDistance, so the walk has to end
                // there too, or the creature would stop at the wider tree/building reach and
                // never get close enough to take anything.
                var reach = food.Category == EntityCategory.Pile ? Configuration.Rules.PileReachDistance : reachDistance;
                return new GatherTask(food, reach);
            }
        }

        // The two animal food steps trigger earlier, at WouldEatIfTheyCould's lower threshold: a
        // hunt is a long trip, worth setting out on well before hunger turns urgent, unlike the
        // plant food/pile search just above (which keeps the higher threshold on purpose). Still
        // tried only once the plant food/pile search above has come up with nothing, and
        // butchering before hunting when both are known: a carcass already on the ground is a
        // meal without the risk of a miss, and wiping out a whole hunt's worth of throws over a
        // herd that already has food lying around would be busywork.
        if (WouldEatIfTheyCould(creature))
        {
            if (IsKnownSkill(creature, ButcherCommand.Skill) && FindNearestDeadAnimalWithMeat(searchOrigin) is { } carcass)
            {
                return new ButcherTask(carcass, Configuration.Rules.PileReachDistance, GatherTask.SpeedPerTick);
            }

            if (IsKnownSkill(creature, HuntCommand.Skill) && FindNearestHuntablePrey(searchOrigin) is { } prey)
            {
                return new HuntTask(prey, Configuration.Rules.HuntingRange, GatherTask.SpeedPerTick);
            }
        }

        // Nearest wins regardless of which known skill it needs. IsKnownSkill checks the skill's
        // BaseTechnique, since KnownTechniques holds arbitrary techniques rather than skills.
        var node = FindNearestGatherableEntity(creature, searchOrigin, definition => IsKnownSkill(creature, definition.Skill));
        if (node is not null)
        {
            return new GatherTask(node, reachDistance);
        }

        // Null for a creature with no home (every Person today), exactly IdleTask's own default;
        // an Animal's home range is what its wander legs and radius come from instead.
        return new IdleTask(creature.Home);
    }

    private bool IsKnownSkill(Creature creature, SkillTypeId skill)
    {
        var definition = Configuration.SkillCatalog.Find(skill);
        return definition is not null && creature.KnownTechniques.Contains(definition.BaseTechnique);
    }

    // Null for a species with no Flee (every human), or one with Flee but nobody living within
    // FleeDistance right now. The nearest living person, not merely "any" - so a herd scattered
    // beside several people always flees the closer one, which is also who KeepsCurrentTask
    // compares against to avoid restarting a flee that is already running from the same threat.
    private Person? NearbyThreatTo(Creature creature)
    {
        if (Configuration.SpeciesCatalog.Get(creature.Species).Flee is not { } flee)
        {
            return null;
        }

        var nearest = _people.Where(p => p.IsAlive).MinBy(p => Distance(creature.Position, p.Position));
        return nearest is not null && Distance(creature.Position, nearest.Position) < flee.FleeDistance ? nearest : null;
    }

    private bool IsNursedBy(Creature creature, Creature? mother) =>
        creature.IsAlive
        && mother is { IsAlive: true }
        && ReferenceEquals(creature.NursingMother, mother)
        && LifeStageOf(creature) == LifeStage.Infant
        && IsWithinReach(creature.Position, mother.Position);

    // Food for a given creature, not food in general, the same distinction IsFoodPile makes.
    private bool IsFoodResource(Creature creature, ResourceDefinition definition) =>
        definition.YieldsItem is { } item && HungerRestoredPerUnitFor(creature, item) > 0f;

    private bool HasEdibleFood(Creature creature) =>
        creature.Inventory.Counts.Any(kv => kv.Value > 0 && HungerRestoredPerUnitFor(creature, kv.Key) > 0f);

    // Depleted-but-alive nodes (RemainingAmount 0, regenerating) are skipped - a fuller one of
    // the same kind is normally nearby - and so is anything this creature could not take from:
    // nobody walks to a source to gather nothing. `origin` only bounds the *fallback* search's
    // reach - the creature's own position for a Person, its HomeRange anchor for an Animal with
    // nothing matching inside its home - the *nearest* pick in both tiers is always
    // nearest-to-the-creature-itself, not to the anchor.
    //
    // An animal with a Home searches nearest-to-itself among nodes bounded by its Home (radius
    // plus a small margin for its own footprint), not nearest-to-the-shared-anchor: the anchor
    // bounds where the herd may graze, it is not everybody's common destination. Picking nearest
    // to the anchor instead sent every member of a herd at the single node nearest that one point,
    // where ResolveCollisions then kept most of them outside MaxInteractionDistance and nobody
    // ate.
    //
    // The in-home tier also only counts a node that can still give this creature a full harvest,
    // not merely IsWorthGathering's "more than zero left": a home tuft that regrew to a sliver
    // still "matched" the plain rule, so a herd converged on its own barely-regrown patch and
    // nibbled it at regen speed forever rather than falling through to fuller grass a little
    // further out. Only when nothing at home clears that bar does the search widen to the wider
    // IdleSearchRadius tier, at the any-amount-left rule - still picking whichever match is
    // nearest to the creature itself (not to the anchor), or a herd already scattered across its
    // own ground by the first tier would regroup on a single depleted tuft nearest the anchor the
    // moment it fell through to this one. For a Person (no Home) `origin` is its own position
    // anyway, so both tiers agree.
    private Entity? FindNearestGatherableEntity(Creature creature, Position origin, Func<ResourceDefinition, bool> matches)
    {
        if (creature.Home is { } home)
        {
            var margin = Configuration.SpeciesCatalog.Get(creature.Species).CollisionRadius * 2f;
            var nearestAtHome = NearestGatherableEntity(
                creature,
                nearestTo: creature.Position,
                matches,
                inBounds: entity => Distance(home.Anchor, entity.Position) <= home.Radius + margin && HasAFullHarvestFor(creature, entity));
            if (nearestAtHome is not null)
            {
                return nearestAtHome;
            }
        }

        return NearestGatherableEntity(creature, nearestTo: creature.Position, matches, inBounds: entity => Distance(origin, entity.Position) <= Configuration.Rules.IdleSearchRadius);
    }

    // IsWorthGathering has already confirmed entity.Growth is alive by the time this runs
    // (NearestGatherableEntity checks it first), so Growth here is never null.
    private bool HasAFullHarvestFor(Creature creature, Entity entity) =>
        GatherCommand.WouldYieldAFullHarvest(this, creature, Configuration.ResourceCatalog.Get(entity.Kind), entity.Growth!.RemainingAmount);

    private Entity? NearestGatherableEntity(Creature creature, Position nearestTo, Func<ResourceDefinition, bool> matches, Func<Entity, bool> inBounds)
    {
        Entity? nearest = null;
        var nearestDistance = double.MaxValue;
        foreach (var entity in _entities)
        {
            if (!IsWorthGathering(creature, entity) || !matches(Configuration.ResourceCatalog.Get(entity.Kind)) || !inBounds(entity))
            {
                continue;
            }

            var distance = Distance(nearestTo, entity.Position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = entity;
            }
        }

        return nearest;
    }

    private Entity? FindNearestFoodPile(Creature creature, Position origin) =>
        _entities
            .Where(pile => IsFoodPile(creature, pile))
            .Where(pile => Distance(origin, pile.Position) <= Configuration.Rules.IdleSearchRadius)
            .MinBy(pile => Distance(origin, pile.Position));

    // A carcass worth walking to for its meat, nearest to `origin` first - "worth" meaning
    // ButcherCommand would actually find something, not merely that an animal died here once.
    // Only meat is asked about: a picked-clean carcass still holding hide or bone but no meat is
    // not a meal.
    private Animal? FindNearestDeadAnimalWithMeat(Position origin) =>
        _animals
            .Where(animal => !animal.IsAlive && animal.Inventory.Get(ButcherCommand.Meat) > 0)
            .Where(animal => Distance(origin, animal.Position) <= Configuration.Rules.IdleSearchRadius)
            .MinBy(animal => Distance(origin, animal.Position));

    // Living prey worth a throw, nearest to `origin` first: a species whose carcass would hold
    // no meat at all is not worth hunting - nothing here checks FleeDistance or HuntingRange,
    // since HuntTask itself closes whatever gap remains.
    private Animal? FindNearestHuntablePrey(Position origin) =>
        _animals
            .Where(animal => animal.IsAlive && Configuration.SpeciesCatalog.Get(animal.Species).Carcass.Any(yield => yield.Item == ButcherCommand.Meat && yield.Amount > 0))
            .Where(animal => Distance(origin, animal.Position) <= Configuration.Rules.IdleSearchRadius)
            .MinBy(animal => Distance(origin, animal.Position));

    private static Entity? NearerOf(Position origin, Entity? a, Entity? b) => (a, b) switch
    {
        (null, _) => b,
        (_, null) => a,
        _ => Distance(origin, a.Position) <= Distance(origin, b.Position) ? a : b,
    };

    // What people standing together say to each other about the stuff of the world. Talk, not
    // instruction: nobody needs to know how to teach to mention that a stone shatters, which is
    // why this does not go through TeachCommand and why understanding can spread before anyone
    // has learned to teach at all.
    //
    // What is passed on is only what the teller would act on themselves, and it lands as
    // hearsay - held less firmly than what the listener could have found out by handling it.
    // So one mention is talk and two are conviction, and somebody who then handles the stuff
    // settles the matter for themselves. Today the account passed on is always true; the
    // listener's only doubt is how often they have heard it.
    private void ShareWhatTheyKnow(long currentTick)
    {
        var rules = Configuration.Rules;

        foreach (var teller in _people)
        {
            if (!teller.IsAlive)
            {
                continue;
            }

            foreach (var listener in _people)
            {
                if (ReferenceEquals(listener, teller)
                    || !listener.IsAlive
                    || !IsWithinReach(teller.Position, listener.Position))
                {
                    continue;
                }

                MentionSomething(teller, listener, rules, currentTick);
            }
        }
    }

    // One thing per pair per tick, as casual teaching passes at most one technique: a
    // conversation, not a lecture.
    private static void MentionSomething(Person teller, Person listener, SimulationRules rules, long currentTick)
    {
        // Ordered, because a dictionary's own order is nobody's promise and this has to replay
        // the same way twice.
        foreach (var held in teller.Beliefs.Held.OrderBy(entry => entry.Key.Material.Value, StringComparer.Ordinal).ThenBy(entry => entry.Key.Property))
        {
            var (material, property) = held.Key;

            // People repeat what they were told, not only what they have proved for themselves -
            // which is what lets a tale travel A to B to C, gathering error at every hop (see
            // docs/knowledge-transmission-architecture.md section 1). A vague inkling from
            // having once brushed past the stuff is still beneath saying out loud, so the bar
            // is what a telling itself is worth.
            if (teller.Beliefs.ConfidenceIn(material, property) < rules.HearsayConfidence
                || listener.Beliefs.ConfidenceIn(material, property) >= teller.Beliefs.ConfidenceIn(material, property))
            {
                continue;
            }

            if (!PassesBeliefSharingRoll(teller, listener, material, property, currentTick, rules.BeliefSharingChancePerTick))
            {
                continue;
            }

            var asTold = Distorted(held.Value.Value, teller, listener, material, property, currentTick, rules);
            listener.Beliefs.Learn(material, property, asTold, rules.HearsayConfidence);
            return;
        }
    }

    // What the listener takes away, which is not quite what was said. Nobody tells it better
    // than they know it, so the error is laid on top of whatever the teller already believed -
    // that is what makes it accumulate along a chain rather than being one lossy event (see
    // docs/knowledge-transmission-architecture.md section 1).
    //
    // A practised teacher narrows it to nothing, which is the lever the player has: train
    // somebody to teach, or send people to find things out first-hand. Nothing here marks a
    // belief as wrong, and nobody holding one can tell (section 6) - two people simply come to
    // disagree, and reality settles it when somebody next works the stuff.
    private static float Distorted(float told, Person teller, Person listener, MaterialId material, MaterialProperty property, long currentTick, SimulationRules rules)
    {
        var fidelity = WorkAttempt.Practised(teller, TeachCommand.TeachingSkill);
        var reach = rules.HearsayDistortion * (1f - fidelity);
        if (reach <= 0f)
        {
            return told;
        }

        var mixed = unchecked((uint)(teller.Id.Seed * 2654435761u)
                              ^ (uint)(listener.Id.Seed * 40503)
                              ^ (uint)(StableStringHash(material.Value) * 19349663)
                              ^ (uint)((int)property * 83492791)
                              ^ ((uint)currentTick * 73856093u));

        var drift = ((float)new Random(SeedHash.Avalanche(mixed)).NextDouble() * 2f) - 1f;

        // Never below nothing: a substance cannot be less than not fibrous at all. No ceiling,
        // because density is not on a 0-1 scale and a tall tale about how heavy stone is should
        // be tellable.
        return Math.Max(0f, told + (drift * reach));
    }

    // Deterministic from the pair, what is being said and the tick, as every other roll is.
    private static bool PassesBeliefSharingRoll(Person teller, Person listener, MaterialId material, MaterialProperty property, long currentTick, float chance)
    {
        var mixed = unchecked((uint)(teller.Id.Seed * 73856093)
                              ^ (uint)(listener.Id.Seed * 19349663)
                              ^ (uint)(StableStringHash(material.Value) * 83492791)
                              ^ (uint)((int)property * 2654435761u)
                              ^ ((uint)currentTick * 40503u));

        // Stryker disable once Equality: NextDouble() returning exactly the chance has
        // probability zero, so < and <= are the same roll
        return new Random(SeedHash.Avalanche(mixed)).NextDouble() < chance;
    }

    // Handling a thing teaches what it is like. Nobody is told that grass is fibrous; they
    // carry it about and come to know. A person's understanding of the world is therefore the
    // sum of what they have actually had in their hands, which is why a band that never picks
    // anything up learns nothing about anything.
    private void LearnWhatIsInHand()
    {
        var gained = Configuration.Rules.MaterialUnderstandingPerTick;

        foreach (var person in _people)
        {
            if (!person.IsAlive)
            {
                continue;
            }

            foreach (var material in MaterialsInHand(person))
            {
                if (Configuration.MaterialCatalog.Find(material) is not { } actual)
                {
                    continue;
                }

                // Learned true: nothing distorts a belief yet, and what is noticed first-hand
                // would be the last thing to.
                person.Beliefs.LearnAll(actual, gained);
            }
        }
    }

    private IEnumerable<MaterialId> MaterialsInHand(Person person)
    {
        var items = Configuration.ItemCatalog;

        return person.Inventory.Counts.Keys
            .Select(kind => items.Get(kind).Material)
            .Concat(person.Inventory.Assemblies.SelectMany(PartMaterialsOf))
            .Distinct();
    }

    // Every substance in a made thing, however deep: somebody carrying a hafted axe about has
    // their hands on both the stone and the wood.
    private static IEnumerable<MaterialId> PartMaterialsOf(Assembly assembly) => assembly switch
    {
        Assembly.Part part => [part.Material],
        Assembly.Joined joined => PartMaterialsOf(joined.Left).Concat(PartMaterialsOf(joined.Right)),
        _ => [],
    };

    // Idle hands turning something over, and now and then working out how it is done (see
    // docs/materials-and-crafting-architecture.md section 7, "idle experimentation"). This is
    // what keeps knowledge living in people rather than in the player's head: a settlement left
    // alone still develops, and a band that comes after an extinction re-derives things for
    // itself instead of waiting to be shown.
    //
    // The rule is one sentence: a person works out how to do the thing they could have done
    // already, if only they had known how. Each verb's own command is asked what stands in the
    // way, and discovery happens exactly when the answer is "nothing but not knowing" - so
    // nothing here re-states what a verb needs, and a verb that grows a new requirement is
    // obeyed here for free.
    //
    // Undirected, unlike the workbench: what is tried is whatever is in their hands, taken at
    // random, and they do not choose it. The player who aims an attempt is buying aim, which is
    // what makes directing worth the time it costs (section 7, "How the two paths differ").
    private void DiscoverByFiddling(long currentTick)
    {
        foreach (var person in _people)
        {
            // Only genuinely idle hands: somebody walking somewhere or working a resource is
            // busy with that, and a person nobody has taught to be anywhere is the one with
            // time to turn a thing over.
            if (!person.IsAlive || person.Tasks.Current is not IdleTask)
            {
                continue;
            }

            if (TrialOf(person, currentTick) is not { } trial)
            {
                continue;
            }

            var (skill, command) = trial;
            if (command.Blocker(this) is not ActionBlocker.NotLearned)
            {
                continue;
            }

            if (Configuration.SkillCatalog.Find(skill) is { } definition
                && PassesIdleDiscoveryRoll(person, skill, currentTick))
            {
                person.KnownTechniques.Add(definition.BaseTechnique);
            }
        }
    }

    // What this person happens to be turning over this tick: one thing out of the pack, or two.
    // Drawn from the same seeded stream as every other autonomous roll, so a replay fiddles with
    // the same things in the same order.
    // Only what they understand. Idle hands turn over the familiar, so a substance nobody has
    // yet come to know is not one they will idly think to work - the player can direct an
    // attempt on anything, and that difference in *reach* is what directing buys beyond speed
    // (docs/materials-and-crafting-architecture.md section 7).
    private (SkillTypeId Skill, ICommand Command)? TrialOf(Person person, long currentTick)
    {
        var stock = person.Inventory.Counts.Keys
            .Where(kind => person.Beliefs.HoldsAnythingAbout(Configuration.ItemCatalog.Get(kind).Material))
            .OrderBy(kind => kind.Value, StringComparer.Ordinal)
            .ToList();
        var worked = person.Inventory.Assemblies;
        var things = stock.Count + worked.Count;
        if (things == 0)
        {
            return null;
        }

        var rng = new Random(SeedHash.Avalanche(unchecked((uint)(person.Id.Seed * 40503) ^ ((uint)currentTick * 2654435761u))));

        // Two things in hand is a chance to wonder what they would be together; one is a chance
        // to wonder what it would be on its own. With only one thing there is nothing to bind.
        if (things > 1 && rng.Next(2) == 0)
        {
            var left = TargetAt(stock, worked, rng.Next(things));
            var right = TargetAt(stock, worked, rng.Next(things));

            return (BindCommand.Skill, new BindCommand(person, left, right));
        }

        // Otherwise they turn one thing over by itself: raw stock gets worked down, and something
        // already made gets worked over, which today means its edge renewed. Either may come to
        // nothing - stuff that answers to no reductive verb, or a made thing with no edge on it.
        return WorkingOverOneThing(person, TargetAt(stock, worked, rng.Next(things)));
    }

    private (SkillTypeId Skill, ICommand Command)? WorkingOverOneThing(Person person, CarriedThing picked) => picked switch
    {
        CarriedThing.Stock stock => ReductiveVerbs.For(person, stock.Kind, Configuration.ItemCatalog),
        CarriedThing.Worked worked when SharpenCommand.HasAnEdge(worked.Thing, this) =>
            (SharpenCommand.Skill, new SharpenCommand(person, worked.Thing)),
        _ => null,
    };

    // Concrete List rather than the interface: the caller has one, and indexing through
    // IReadOnlyList costs an interface dispatch per pick (CA1859).
    private static CarriedThing TargetAt(List<ItemKindId> stock, IReadOnlyList<Assembly> worked, int index) =>
        index < stock.Count
            ? new CarriedThing.Stock(stock[index])
            : new CarriedThing.Worked(worked[index - stock.Count]);

    // Deterministic from the person, the verb and the tick, as every other roll is. A person's
    // own Curiosity scales it, which is the knob an NPC band turns down.
    private bool PassesIdleDiscoveryRoll(Person person, SkillTypeId skill, long currentTick)
    {
        var chance = Configuration.Rules.IdleDiscoveryChancePerTick * person.Curiosity;
        var mixed = unchecked((uint)(person.Id.Seed * 2246822519) ^ (uint)(StableStringHash(skill.Value) * 3266489917) ^ ((uint)currentTick * 668265263u));

        // Stryker disable once Equality: NextDouble() returning exactly the chance has
        // probability zero, so < and <= are the same roll
        return new Random(SeedHash.Avalanche(mixed)).NextDouble() < chance;
    }

    // Once somebody knows a technique and how to teach, anyone nearby may pick it up without a
    // player action; SimulationRules.CasualTeachingChancePerTick says why it is a per-tick roll.
    // Every living pair every tick: O(n^2) is negligible at tens of people.
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
    // business; this pass adds only the bond threshold. Iterates a snapshot because BirthCommand
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

    // Where fawns come from when nobody asks - the animal counterpart of StartFamilies, but no
    // pair state: a female's own id and the tick decide everything. Iterates a snapshot of
    // _animals because giving birth adds to it, the same "a child must not become a candidate on
    // the tick it is born" guard StartFamilies uses.
    private void BreedAnimals(long currentTick, Climate climate)
    {
        var mothers = _animals.Where(animal => animal.IsAlive && animal.Sex == Sex.Female).ToList();

        foreach (var mother in mothers)
        {
            if (Configuration.SpeciesCatalog.Get(mother.Species).Breeding is not { } breeding)
            {
                continue;
            }

            if (mother.PregnantSinceTick is { } pregnantSinceTick)
            {
                if (currentTick - pregnantSinceTick >= breeding.GestationTicks)
                {
                    GiveBirth(mother, currentTick);
                }

                continue;
            }

            if (LifeStageOf(mother) != LifeStage.Adult
                || NursingInfantOf(mother) is not null
                || mother.Needs.Hunger >= breeding.SatietyHungerBelow
                || climate != breeding.Climate
                || !HasAdultMaleOfHerOwnSpeciesAtHome(mother))
            {
                continue;
            }

            if (PassesConceptionRoll(mother, currentTick, breeding.ConceptionChancePerTick))
            {
                mother.PregnantSinceTick = currentTick;
            }
        }
    }

    // Same HomeRange reference, not merely nearby - a herd shares one anchor, so "at home" is
    // exactly "in this herd" rather than a distance check.
    private bool HasAdultMaleOfHerOwnSpeciesAtHome(Animal mother) =>
        _animals.Any(candidate =>
            candidate.IsAlive
            && candidate.Sex == Sex.Male
            && candidate.Species == mother.Species
            && ReferenceEquals(candidate.Home, mother.Home)
            && LifeStageOf(candidate) == LifeStage.Adult);

    // What spawning does for a fawn born mid-game rather than drawn at map load: same position
    // and Home as its mother, her as Mother, sex off its own freshly drawn id, and whatever the
    // species starts every newborn knowing.
    private void GiveBirth(Animal mother, long currentTick)
    {
        var id = NewbornIdFor(mother.Id, currentTick);
        Execute(new SpawnAnimalCommand(id, mother.Species, mother.Position, mother.Home, Creature.SexOf(id), currentTick, mother));
        mother.PregnantSinceTick = null;
    }

    // Deterministic from the mother's own id and the tick, as NameForNewborn is from two parents'
    // ids and the tick - a fawn has only one parent in this rule, so her id alone is the seed.
    private static CreatureId NewbornIdFor(CreatureId motherId, long currentTick)
    {
        var mixed = unchecked((uint)(motherId.Seed * 2654435761u) ^ ((uint)currentTick * 40503u));
        return CreatureId.New(new Random(SeedHash.Avalanche(mixed)));
    }

    // Deterministic from the mother and the tick, as every other roll is.
    private static bool PassesConceptionRoll(Animal mother, long currentTick, float chance)
    {
        var mixed = unchecked((uint)(mother.Id.Seed * 73856093) ^ ((uint)currentTick * 19349663u));

        // Stryker disable once Equality: NextDouble() returning exactly `chance` has probability
        // zero, so < and <= are the same roll
        return new Random(SeedHash.Avalanche(mixed)).NextDouble() < chance;
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

    // Deterministic from the parents and the tick, so a replayed world names the same children.
    // The culture it draws from is rebuilt from People/Forebears rather than saved separately.
    //
    // Public because a child the player asks for is named the same way as one the band has of
    // its own accord: BirthCommand takes the name, so somebody has to draw it, and there is only
    // one right way to draw it.
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
    // rather than a curated pool - what the player's manual "Spawn Person" button uses.
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

    // Deterministic from the ids' seeds and the tick, rather than a shared Random: reproducible
    // and independent of call order between people.
    private static bool PassesCasualTeachingRoll(CreatureId teacherId, CreatureId studentId, TechniqueId technique, long tick, float chance)
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

    // What a dead creature leaves behind, put into its own Inventory once at the moment it dies -
    // whatever the cause, hunger and old age included, and however starved or old it died:
    // scaling the yield by condition is left for later, noted but not solved here. A human's
    // species carries no Carcass at all, so this adds nothing to a dead person; taking a dead
    // person's possessions stays LootCommand's job.
    // Internal rather than private: HuntCommand's kill is a death caused by a command rather
    // than by this Advance's own hunger/old-age check, but it fills a carcass exactly the same
    // way.
    internal void FillCarcass(Creature creature)
    {
        // The moment these came to be as things is the death itself, whichever path set it
        // moments ago - hunger/old age (using the tick this death happened on) or a hunt (which
        // only has world.Clock.CurrentTick to hand). Falls back to now only if somehow called
        // before DeathTick was set.
        var tick = creature.DeathTick ?? Clock.CurrentTick;
        var itemCatalog = Configuration.ItemCatalog;
        foreach (var yield in Configuration.SpeciesCatalog.Get(creature.Species).Carcass)
        {
            creature.Inventory.Add(yield.Item, yield.Amount, tick, itemCatalog);
        }
    }

    // Whether this creature's corpse has crossed SimulationRules.CorpseDecayTicks - derived
    // rather than stored. A living creature, or one that never died in this world (no
    // DeathTick), is never decayed. BuryCommand asks this to tell an
    // unmarked grave from a marked one; the >= here (as opposed to Advance's own one-time ==)
    // is deliberate, since a caller may ask on any tick, not just the one decay happened on.
    public bool IsDecayed(Creature creature) =>
        creature.DeathTick is { } deathTick && Clock.CurrentTick - deathTick >= Configuration.Rules.CorpseDecayTicks;

    // Same behaviour as the player's Eat button: eats through whatever food is on hand until no
    // longer hungry. Runs every tick whatever task is active, even a player-issued one - a
    // starving creature should not wait for a free moment to eat from their own pack.
    private void TryAutoEat(Creature creature)
    {
        // A meal, not a nibble: nothing until hunger has built up, then EatCommand eats to zero.
        if (!IsHungryEnoughToEat(creature))
        {
            return;
        }

        foreach (var kind in creature.Inventory.Counts.Keys.ToList())
        {
            // Stryker disable once Equality,Statement,Block: EatCommand no-ops at zero hunger anyway, so this only saves the remaining calls
            if (creature.Needs.Hunger <= 0f)
            {
                break;
            }

            new EatCommand(creature, kind).Execute(this);
        }
    }

    // MoveTask/IdleTask aim at a destination with no awareness of what else is there, so this
    // untangles the overlap afterwards, every tick (O(n^2), as AutoTeachNearbyPeople). Every
    // living creature, person or animal - a deer and a person are pushed apart using their own
    // species' radii rather than one shared constant. Separations are computed against
    // start-of-tick positions and summed into one clamped push per creature
    // (SimulationRules.MaxCollisionPushPerTick), so discovery order cannot bias the result.
    private void ResolveCollisions()
    {
        var speciesCatalog = Configuration.SpeciesCatalog;
        var maxPushPerTick = Configuration.Rules.MaxCollisionPushPerTick;
        var resourceCatalog = Configuration.ResourceCatalog;
        var creatures = AllCreatures().Where(creature => creature.IsAlive).ToList();
        var radii = creatures.Select(creature => speciesCatalog.Get(creature.Species).CollisionRadius).ToList();
        var pushes = new (double X, double Y)[creatures.Count];

        for (var i = 0; i < creatures.Count; i++)
        {
            for (var j = i + 1; j < creatures.Count; j++)
            {
                if (!TrySeparation(creatures[i].Position, creatures[j].Position, radii[i] + radii[j], out var pushX, out var pushY))
                {
                    continue;
                }

                pushes[i] = (pushes[i].X + (pushX / 2), pushes[i].Y + (pushY / 2));
                pushes[j] = (pushes[j].X - (pushX / 2), pushes[j].Y - (pushY / 2));
            }
        }

        for (var i = 0; i < creatures.Count; i++)
        {
            var creature = creatures[i];
            var (pushX, pushY) = pushes[i];
            foreach (var entity in _entities)
            {
                if (entity.Growth is not { IsAlive: true })
                {
                    continue;
                }

                var collisionRadius = resourceCatalog.Get(entity.Kind).CollisionRadius;
                if (collisionRadius <= 0f
                    || !TrySeparation(creature.Position, entity.Position, radii[i] + collisionRadius, out var nodePushX, out var nodePushY))
                {
                    continue;
                }

                pushX += nodePushX;
                pushY += nodePushY;
            }

            ApplyClampedPush(creature, pushX, pushY, maxPushPerTick);
        }
    }

    private static void ApplyClampedPush(Creature creature, double pushX, double pushY, float maxPushPerTick)
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

        creature.Position = new Position(creature.Position.X + pushX, creature.Position.Y + pushY);
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

    internal void RestoreAnimal(Animal animal) => _animals.Add(animal);

    internal void RestoreEntity(Entity entity) => _entities.Add(entity);

    internal void RestoreGrave(Grave grave) => _graves.Add(grave);

    internal void RestoreHomeRange(HomeRange homeRange) => _homeRanges.Add(homeRange);
}
