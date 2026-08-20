using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Continuity;

// An unmarked grave (IsMarked: false) deliberately carries no identity - a burial performed
// without the practiced technique doesn't preserve who the person was, only that someone was
// laid to rest here. Everything below stays null/empty for an unmarked grave - name, age, and
// lineage are name/value snapshots taken at burial time, not live references, so the record
// stays meaningful even after a referenced parent is later buried themselves.
public sealed class Grave
{
    public required GraveId Id { get; init; }

    public required Position Position { get; init; }

    public required bool IsMarked { get; init; }

    public string? Name { get; init; }

    public int? AgeAtDeath { get; init; }

    public DeathCause? CauseOfDeath { get; init; }

    public string? MotherName { get; init; }

    public string? FatherName { get; init; }

    public IReadOnlyList<TechniqueId> KnownTechniques { get; init; } = [];
}
