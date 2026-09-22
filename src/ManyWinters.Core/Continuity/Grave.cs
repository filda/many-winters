using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Continuity;

// An unmarked grave (IsMarked: false) carries no identity: a burial without the practiced
// technique preserves only that someone lies here. Name, age and lineage are snapshots taken
// at burial, not live references, so the record outlives the people it names.
public sealed class Grave
{
    public GraveId Id { get; init; } = GraveId.New();

    public required Position Position { get; init; }

    public required bool IsMarked { get; init; }

    public string? Name { get; init; }

    public Sex? Sex { get; init; }

    public int? AgeAtDeath { get; init; }

    public DeathCause? CauseOfDeath { get; init; }

    public string? MotherName { get; init; }

    public string? FatherName { get; init; }

    public IReadOnlyList<TechniqueId> KnownTechniques { get; init; } = [];
}
