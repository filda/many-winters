namespace ManyWinters.Core.Population;

// How much a person can carry, before any equipped gear bonus (see
// WorldState.MaxCarryWeightFor, which adds ItemDefinition.CarryCapacityBonus on top) - rises
// from a small fraction at birth up to the full adult baseline, holds there through the
// prime years, then eases back down a little toward the end of a lifespan rather than
// staying at its physical peak forever. The ages it turns at are LifeStages' - the same ones
// that decide who is still nursing and who can have children.
public static class CarryCapacity
{
    public const float AdultBaseWeight = 50f;

    private const float NewbornFraction = 0.2f;
    private const float ElderEndFraction = 0.85f;

    public static float BaseWeightFor(long ageInYears, long maxLifespanYears)
    {
        // Stryker disable once Equality: at age 0 the growth branch below works out to exactly
        // NewbornFraction anyway, so <= 0 and < 0 return the same weight
        if (ageInYears <= 0)
        {
            return AdultBaseWeight * NewbornFraction;
        }

        // Stryker disable once Equality: at AdultAgeYears the growth factor is exactly 1, so
        // the growth branch and the adult baseline below agree - < and <= are indistinguishable
        if (ageInYears < LifeStages.AdultAgeYears)
        {
            var growth = ageInYears / (float)LifeStages.AdultAgeYears;
            return AdultBaseWeight * (NewbornFraction + ((1f - NewbornFraction) * growth));
        }

        // Stryker disable once Equality: at ElderAgeYears the decline is exactly 0, so the
        // decline branch below also returns the full adult baseline
        if (ageInYears < LifeStages.ElderAgeYears)
        {
            return AdultBaseWeight;
        }

        var declineSpan = Math.Max(1, maxLifespanYears - LifeStages.ElderAgeYears);
        var decline = Math.Min(1f, (ageInYears - LifeStages.ElderAgeYears) / (float)declineSpan);
        return AdultBaseWeight * (1f - ((1f - ElderEndFraction) * decline));
    }
}
