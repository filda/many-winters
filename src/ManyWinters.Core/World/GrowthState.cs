namespace ManyWinters.Core.World;

// A resource's life cycle: how much is left to harvest, whether it is still standing, and how
// long it has been suffering a climate it cannot survive (see WorldState.Advance). Only entities
// with EntityCategory.Growable carry one - a dropped pile or a building has nothing here.
public sealed class GrowthState
{
    public float RemainingAmount { get; set; }

    public float MaxAmount { get; init; }

    public bool IsAlive { get; set; } = true;

    public long? DeathTick { get; set; }

    public ResourceDeathCause? CauseOfDeath { get; set; }

    // Ticks spent in a row in an inhospitable climate (see ResourceDefinition.IsInhospitable),
    // reset back to zero as soon as the climate turns hospitable again.
    public float ColdStress { get; set; }
}
