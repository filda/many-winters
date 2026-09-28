using ManyWinters.Core.Commands;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.Population.Naming;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.Time;

namespace ManyWinters.Core.World;

public sealed class WorldState
{
    // A floor, not a tuning knob - a building can't be in negative repair.
    private const float MinCondition = 0f;

    private readonly List<Person> _people = new();
    private readonly List<Person> _forebears = new();
    private readonly List<Animal> _animals = new();
    private readonly List<Entity> _entities = new();
    private readonly List<Grave> _graves = new();
    private readonly List<HomeRange> _homeRanges = new();

    // Naming's live People/Forebears references make this an ordinary constructor rather than a
    // primary one: a field initializer cannot refer to another instance field.
    public WorldState(WorldConfiguration configuration)
    {
        Configuration = configuration;
        Naming = new NamingCulture(
            _people,
            _forebears,
            Configuration.Rules.CultureDecayPerObservation,
            Configuration.Rules.RecentTrendWindow,
            Configuration.Rules.CultureWeight,
            Configuration.Rules.TrendWeight,
            Configuration.Rules.ParentWeight
            );
    }

    public SimulationClock Clock { get; } = new();

    public ExplorationState Exploration { get; } = new();

    // What any two people mean to each other. A record about pairs, not a list of things in the
    // world, so it has no Add* and announces nothing - the inspector reads it when it draws.
    public Affections Affections { get; } = new();

    // The words this band has for the things it makes. Empty at the start: nobody has made
    // anything, so there is nothing to have a word for.
    public Vocabulary Vocabulary { get; } = new();

    // What this band calls its newborns, rebuilt from People/Forebears rather than saved on its
    // own.
    public NamingCulture Naming { get; }

    // Catalogs, calendar and tuning numbers - fixed for the world's lifetime and not part of a
    // save file.
    public WorldConfiguration Configuration { get; }

    public IReadOnlyList<Person> People => _people;

    // People who died before the story began and exist only to be somebody's parent: full
    // Person objects a grave or a save file can refer to, but never in People - nothing
    // simulates, draws, counts or clicks them.
    public IReadOnlyList<Person> Forebears => _forebears;

    // The second kind of Creature: simulated in Advance alongside People, but never a target of
    // the people-only passes (casual teaching, affection, family-starting, ...).
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

    // A dead, unburied animal whose bones have finally lingered past the bones-linger time -
    // fired by the decay pass in Advance, mirroring EntityRemoved.
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
    // to point at. A growable entity that dies is never removed this way - it stays in Entities,
    // marked no longer alive in its growth state.
    public void RemoveEntity(Entity entity)
    {
        _entities.Remove(entity);
        EntityRemoved?.Invoke(entity);
    }

    // Bones gone into the ground: the decay pass calls this once its bones have lingered past
    // the bones-linger time. Never called for a Person - see AnimalRemoved.
    internal void RemoveAnimal(Animal animal)
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

    // The one "hungry enough to bother" test, shared by eating from one's own pack and eating at
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
    public LifeCycle LifeCycleOf(Creature creature) => Configuration.SpeciesCatalog.Get(creature.Species).LifeCycle;

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
    // for anything (NursingInfantOf, collision resolution) that has to look across both.
    private IEnumerable<Creature> AllCreatures() => _people.Cast<Creature>().Concat(_animals);

    public void Advance(long ticks)
    {
        var rules = Configuration.Rules;
        var seasonParameters = Configuration.SeasonParameters;

        for (var i = 0L; i < ticks; i++)
        {
            // One tick at a time, not the whole batch up front: everything below that reads the
            // clock - a kill's death tick, meat's age, a carcass's decay - has to see the tick it
            // happens on, or a long Advance plays out differently from the same ticks one by one.
            var previousTick = Clock.CurrentTick;
            Clock.Advance();
            var currentTick = Clock.CurrentTick;
            var climate = seasonParameters.ClimateFor(rules.SeasonAt(previousTick));
            var baseHungerMultiplier = seasonParameters.HungerMultiplierFor(climate);
            var regenMultiplier = seasonParameters.RegenMultiplierFor(climate);

            foreach (var creature in AllCreatures())
            {
                if (!creature.IsAlive)
                {
                    continue;
                }

                creature.Tasks.Advance(creature);
                IdleDecision.Reconsider(this, creature, currentTick);

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

                Metabolism.Advance(this, creature, currentTick, baseHungerMultiplier);
            }

            Decay.Advance(this, currentTick);

            CasualTeaching.Advance(this, currentTick);
            IdleDiscovery.LearnWhatIsInHand(this);
            Hearsay.Advance(this, currentTick);
            IdleDiscovery.DiscoverByFiddling(this, currentTick);
            Families.Advance(this, currentTick, climate);
            Collisions.Resolve(this);
            RefreshExploration();

            foreach (var homeRange in _homeRanges)
            {
                homeRange.Advance(currentTick, rules.TicksPerSeason);
            }

            Regrowth.Advance(this, currentTick, climate, regenMultiplier);
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

    private bool IsNursedBy(Creature creature, Creature? mother) =>
        creature.IsAlive
        && mother is { IsAlive: true }
        && ReferenceEquals(creature.NursingMother, mother)
        && LifeStageOf(creature) == LifeStage.Infant
        && IsWithinReach(creature.Position, mother.Position);

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

    // Whether this creature's corpse has crossed the corpse-decay time - derived
    // rather than stored. A living creature, or one that never died in this world (no
    // DeathTick), is never decayed. BuryCommand asks this to tell an
    // unmarked grave from a marked one; the >= here (as opposed to Advance's own one-time ==)
    // is deliberate, since a caller may ask on any tick, not just the one decay happened on.
    public bool IsDecayed(Creature creature) =>
        creature.DeathTick is { } deathTick && Clock.CurrentTick - deathTick >= Configuration.Rules.CorpseDecayTicks;

    private void RefreshExploration() =>
        Exploration.Update(_people.Where(p => p.IsAlive).Select(p => p.Position));

    internal void RestorePerson(Person person) => _people.Add(person);

    internal void RestoreForebear(Person forebear) => _forebears.Add(forebear);

    internal void RestoreAnimal(Animal animal) => _animals.Add(animal);

    internal void RestoreEntity(Entity entity) => _entities.Add(entity);

    internal void RestoreGrave(Grave grave) => _graves.Add(grave);

    internal void RestoreHomeRange(HomeRange homeRange) => _homeRanges.Add(homeRange);
}
