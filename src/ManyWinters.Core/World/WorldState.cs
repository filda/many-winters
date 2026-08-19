using ManyWinters.Core.Commands;
using ManyWinters.Core.Construction;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.Time;

namespace ManyWinters.Core.World;

public sealed class WorldState
{
    private const float HungerPerTick = 1f;
    private const float MaxHunger = 100f;
    private const float ConditionDecayPerTick = 0.05f;
    private const float MinCondition = 0f;

    private readonly List<Person> _people = new();
    private readonly List<ResourceNode> _resourceNodes = new();
    private readonly List<Building> _buildings = new();
    private int _nextPersonId = 1;
    private int _nextResourceNodeId = 1;
    private int _nextBuildingId = 1;

    public WorldState()
        : this(new ResourceCatalog([]), new SkillCatalog([]), new RecipeCatalog([]), new BuildingCatalog([]))
    {
    }

    public WorldState(ResourceCatalog resourceCatalog, SkillCatalog skillCatalog)
        : this(resourceCatalog, skillCatalog, new RecipeCatalog([]), new BuildingCatalog([]))
    {
    }

    public WorldState(ResourceCatalog resourceCatalog, SkillCatalog skillCatalog, RecipeCatalog recipeCatalog)
        : this(resourceCatalog, skillCatalog, recipeCatalog, new BuildingCatalog([]))
    {
    }

    public WorldState(
        ResourceCatalog resourceCatalog,
        SkillCatalog skillCatalog,
        RecipeCatalog recipeCatalog,
        BuildingCatalog buildingCatalog)
    {
        ResourceCatalog = resourceCatalog;
        SkillCatalog = skillCatalog;
        RecipeCatalog = recipeCatalog;
        BuildingCatalog = buildingCatalog;
    }

    public SimulationClock Clock { get; } = new();

    public ResourceCatalog ResourceCatalog { get; }

    public SkillCatalog SkillCatalog { get; }

    public RecipeCatalog RecipeCatalog { get; }

    public BuildingCatalog BuildingCatalog { get; }

    public IReadOnlyList<Person> People => _people;

    public IReadOnlyList<ResourceNode> ResourceNodes => _resourceNodes;

    public IReadOnlyList<Building> Buildings => _buildings;

    public int NextPersonId => _nextPersonId;

    public int NextResourceNodeId => _nextResourceNodeId;

    public int NextBuildingId => _nextBuildingId;

    public event Action<Person>? PersonAdded;

    public event Action<ResourceNode>? ResourceNodeAdded;

    public event Action<Building>? BuildingAdded;

    public Person AddPerson(string name, Position position)
    {
        var person = new Person
        {
            Id = new PersonId(_nextPersonId++),
            Name = name,
            Position = position,
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

    public void Execute(ICommand command) => command.Execute(this);

    public void Advance(long ticks)
    {
        Clock.Advance(ticks);

        foreach (var person in _people)
        {
            var hunger = Math.Min(person.Needs.Hunger + (HungerPerTick * ticks), MaxHunger);
            person.Needs.Hunger = hunger;

            if (hunger >= MaxHunger)
            {
                person.IsAlive = false;
            }
        }

        foreach (var building in _buildings)
        {
            building.Condition = Math.Max(MinCondition, building.Condition - (ConditionDecayPerTick * ticks));
        }
    }

    internal void RestorePerson(Person person) => _people.Add(person);

    internal void SetNextPersonId(int value) => _nextPersonId = value;

    internal void RestoreResourceNode(ResourceNode node) => _resourceNodes.Add(node);

    internal void SetNextResourceNodeId(int value) => _nextResourceNodeId = value;

    internal void RestoreBuilding(Building building) => _buildings.Add(building);

    internal void SetNextBuildingId(int value) => _nextBuildingId = value;
}
