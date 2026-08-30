using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Population;

public sealed class Person
{
    // How much total item weight (see ItemDefinition.Weight) this person can carry at once -
    // gathering/looting/withdrawing all take only as much as still fits (see Inventory's own
    // AddUpToCapacity) rather than overfilling silently or failing outright.
    public const float MaxCarryWeight = 50f;

    public required PersonId Id { get; init; }

    public required string Name { get; init; }

    public Position Position { get; set; }

    public bool IsAlive { get; set; } = true;

    public required long BirthTick { get; init; }

    public long? DeathTick { get; set; }

    public DeathCause? CauseOfDeath { get; set; }

    public bool IsBuried { get; set; }

    public PersonId? MotherId { get; init; }

    public PersonId? FatherId { get; init; }

    public Needs Needs { get; init; } = new();

    public Skills Skills { get; init; } = new();

    public HashSet<TechniqueId> KnownTechniques { get; init; } = new();

    public Inventory Inventory { get; init; } = new();

    public PersonTaskQueue Tasks { get; init; } = new();
}
