using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Population;

public sealed class Person
{
    public required PersonId Id { get; init; }

    public required string Name { get; init; }

    public Position Position { get; set; }

    public bool IsAlive { get; set; } = true;

    public Needs Needs { get; init; } = new();

    public Skills Skills { get; init; } = new();

    public HashSet<TechniqueId> KnownTechniques { get; init; } = new();

    public Inventory Inventory { get; init; } = new();

    public PersonTaskQueue Tasks { get; init; } = new();
}
