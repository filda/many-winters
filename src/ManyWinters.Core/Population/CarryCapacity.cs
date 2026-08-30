namespace ManyWinters.Core.Population;

// How much a person can carry, before any equipped gear bonus (see
// WorldState.MaxCarryWeightFor, which adds ItemDefinition.CarryCapacityBonus on top) - rises
// from a small fraction at birth up to the full adult baseline, holds there through the
// prime years, then eases back down a little toward the end of a lifespan rather than
// staying at its physical peak forever.
public static class CarryCapacity
{
    public const float AdultBaseWeight = 50f;

    private const float NewbornFraction = 0.2f;
    private const long AdultAge = 4;
    private const long ElderAgeStart = 7;
    private const float ElderEndFraction = 0.85f;

    public static float BaseWeightFor(long ageInYears, long maxLifespanYears)
    {
        if (ageInYears <= 0)
        {
            return AdultBaseWeight * NewbornFraction;
        }

        if (ageInYears < AdultAge)
        {
            var growth = ageInYears / (float)AdultAge;
            return AdultBaseWeight * (NewbornFraction + ((1f - NewbornFraction) * growth));
        }

        if (ageInYears < ElderAgeStart)
        {
            return AdultBaseWeight;
        }

        var declineSpan = Math.Max(1, maxLifespanYears - ElderAgeStart);
        var decline = Math.Min(1f, (ageInYears - ElderAgeStart) / (float)declineSpan);
        return AdultBaseWeight * (1f - ((1f - ElderEndFraction) * decline));
    }
}
