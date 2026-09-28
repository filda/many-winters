using ManyWinters.Core.World;

namespace ManyWinters.Core.Population;

// Carry weight before any equipped gear bonus (added elsewhere): grows
// from a fraction at birth to the adult baseline, holds through the prime years, then eases
// down a little toward the end of a lifespan. The turning ages are the creature's own LifeCycle's.
public static class CarryCapacity
{
    public static float MaxCarryWeightFor(
        Creature creature,
        WorldConfiguration configuration,
        long ageInYears,
        LifeCycle lifeCycle
        )
    {
        if (!configuration.SpeciesCatalog.Get(creature.Species).CanCarry)
        {
            return 0f;
        }

        var baseWeight = BaseWeightFor(ageInYears, lifeCycle, configuration.Rules.AdultBaseWeight, configuration.Rules.NewbornFraction, configuration.Rules.ElderEndFraction);
        var gearBonus = creature.Inventory.Counts.Keys.Sum(configuration.ItemCatalog.CarryCapacityBonusFor);
        return baseWeight + gearBonus;
    }

    private static float BaseWeightFor(
        long ageInYears,
        LifeCycle lifeCycle,
        float adultBaseWeight,
        float newbornFraction,
        float elderEndFraction)
    {
        // Stryker disable once Equality: at age 0 the growth branch below works out to exactly
        // newbornFraction anyway, so <= 0 and < 0 return the same weight
        if (ageInYears <= 0)
        {
            return adultBaseWeight * newbornFraction;
        }

        // Stryker disable once Equality: at AdultAgeYears the growth factor is exactly 1, so
        // the growth branch and the adult baseline below agree - < and <= are indistinguishable
        if (ageInYears < lifeCycle.AdultAgeYears)
        {
            var growth = ageInYears / (float)lifeCycle.AdultAgeYears;
            return adultBaseWeight * (newbornFraction + ((1f - newbornFraction) * growth));
        }

        // Stryker disable once Equality: at ElderAgeYears the decline is exactly 0, so the
        // decline branch below also returns the full adult baseline
        if (ageInYears < lifeCycle.ElderAgeYears)
        {
            return adultBaseWeight;
        }

        var declineSpan = Math.Max(1, lifeCycle.MaxLifespanYears - lifeCycle.ElderAgeYears);
        var decline = Math.Min(1f, (ageInYears - lifeCycle.ElderAgeYears) / (float)declineSpan);
        return adultBaseWeight * (1f - ((1f - elderEndFraction) * decline));
    }

}
