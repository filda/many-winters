using ManyWinters.Core.Commands;
using ManyWinters.Core.Construction;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
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

                var insulation = person.Inventory.Counts.Keys.Sum(kind => ItemCatalog.InsulationFor(kind));
                var hungerMultiplier = Math.Max(1f, baseHungerMultiplier - insulation);
                person.Needs.Hunger = Math.Min(person.Needs.Hunger + (HungerPerTick * hungerMultiplier), MaxHunger);

                var age = (currentTick - person.BirthTick) / TicksPerYear;
                var diedOfOldAge = age >= MaxLifespanYears;
                if (person.Needs.Hunger >= MaxHunger || diedOfOldAge)
                {
                    person.IsAlive = false;
                    person.DeathTick = currentTick;
                    person.CauseOfDeath = diedOfOldAge ? DeathCause.OldAge : DeathCause.Hunger;
                }
            }

            foreach (var node in _resourceNodes)
            {
                var regenPerTick = ResourceCatalog.Get(node.Kind).RegenPerTick * regenMultiplier;
                node.RemainingAmount = Math.Min(node.MaxAmount, node.RemainingAmount + regenPerTick);
            }
        }

        foreach (var building in _buildings)
        {
            building.Condition = Math.Max(MinCondition, building.Condition - (ConditionDecayPerTick * ticks));
        }
    }

    private static Season SeasonAt(long tick) => (Season)((tick / TicksPerSeason) % SeasonsPerYear);

    internal void RestorePerson(Person person) => _people.Add(person);

    internal void SetNextPersonId(int value) => _nextPersonId = value;

    internal void RestoreResourceNode(ResourceNode node) => _resourceNodes.Add(node);

    internal void SetNextResourceNodeId(int value) => _nextResourceNodeId = value;

    internal void RestoreBuilding(Building building) => _buildings.Add(building);

    internal void SetNextBuildingId(int value) => _nextBuildingId = value;

    internal void RestoreGrave(Grave grave) => _graves.Add(grave);

    internal void SetNextGraveId(int value) => _nextGraveId = value;
}
