using ManyWinters.Core.Commands;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.Time;

namespace ManyWinters.Core.World;

public sealed class WorldState
{
    private const float HungerPerTick = 1f;
    private const float MaxHunger = 100f;

    private readonly List<Person> _people = new();
    private readonly List<ResourceNode> _resourceNodes = new();
    private int _nextPersonId = 1;
    private int _nextResourceNodeId = 1;

    public WorldState()
        : this(new ResourceCatalog([]), new SkillCatalog([]))
    {
    }

    public WorldState(ResourceCatalog resourceCatalog, SkillCatalog skillCatalog)
    {
        ResourceCatalog = resourceCatalog;
        SkillCatalog = skillCatalog;
    }

    public SimulationClock Clock { get; } = new();

    public ResourceCatalog ResourceCatalog { get; }

    public SkillCatalog SkillCatalog { get; }

    public IReadOnlyList<Person> People => _people;

    public IReadOnlyList<ResourceNode> ResourceNodes => _resourceNodes;

    public int NextPersonId => _nextPersonId;

    public int NextResourceNodeId => _nextResourceNodeId;

    public event Action<Person>? PersonAdded;

    public event Action<ResourceNode>? ResourceNodeAdded;

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
    }

    internal void RestorePerson(Person person) => _people.Add(person);

    internal void SetNextPersonId(int value) => _nextPersonId = value;

    internal void RestoreResourceNode(ResourceNode node) => _resourceNodes.Add(node);

    internal void SetNextResourceNodeId(int value) => _nextResourceNodeId = value;
}
