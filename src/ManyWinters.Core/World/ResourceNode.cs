namespace ManyWinters.Core.World;

public sealed class ResourceNode
{
    public required ResourceNodeId Id { get; init; }

    public required ResourceKindId Kind { get; init; }

    public Position Position { get; set; }

    public float RemainingAmount { get; set; }

    public float MaxAmount { get; set; }

    public bool IsAlive { get; set; } = true;

    public long? DeathTick { get; set; }

    public ResourceDeathCause? CauseOfDeath { get; set; }

    // Ticks spent in a row in an inhospitable climate (see ResourceDefinition.IsInhospitable),
    // reset back to zero as soon as the climate turns hospitable again.
    public float ColdStress { get; set; }
}
