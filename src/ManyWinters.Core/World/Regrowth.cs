namespace ManyWinters.Core.World;

public static class Regrowth
{
    public static void Advance(WorldState world, long currentTick, Climate climate, float regenMultiplier)
    {
        var resourceCatalog = world.Configuration.ResourceCatalog;

        foreach (var entity in world.Entities)
        {
            if (entity.Growth is not { IsAlive: true } growth)
            {
                continue;
            }

            var definition = resourceCatalog.Get(entity.Kind);
            if (definition.IsInhospitable(climate))
            {
                growth.ColdStress += 1f;
                if (growth.ColdStress >= definition.TicksToWither)
                {
                    growth.IsAlive = false;
                    growth.DeathTick = currentTick;
                    growth.CauseOfDeath = ResourceDeathCause.Climate;
                }

                continue;
            }

            growth.ColdStress = 0f;

            var regenPerTick = definition.RegenPerTick * regenMultiplier;
            growth.RemainingAmount = Math.Min(growth.MaxAmount, growth.RemainingAmount + regenPerTick);
        }
    }
}
