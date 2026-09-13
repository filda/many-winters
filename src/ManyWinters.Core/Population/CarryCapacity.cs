namespace ManyWinters.Core.Population;

// Carry weight before any equipped gear bonus (WorldState.MaxCarryWeightFor adds that): grows
// from a fraction at birth to the adult baseline, holds through the prime years, then eases
// down a little toward the end of a lifespan. The turning ages are LifeStages'.
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
