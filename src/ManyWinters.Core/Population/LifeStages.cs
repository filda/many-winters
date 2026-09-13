namespace ManyWinters.Core.Population;

public static class LifeStages
{
    // Still nursing: too young to forage, be taught, or be left behind (WorldState.DecideIdleTask).
    // Absolute winters, not a fraction of MaxLifespanYears: a test world with a three-winter
    // lifespan is one nobody grows up in, not one where childhood lasts four months.
    public const long WeaningAgeYears = 1;

    // Grown: a full load on the back (CarryCapacity), and old enough for children of their own.
    public const long AdultAgeYears = 4;

    // Past their physical peak; they simply carry a little less.
    public const long ElderAgeYears = 7;

    public static LifeStage For(long ageInYears)
    {
        if (ageInYears < WeaningAgeYears)
        {
            return LifeStage.Infant;
        }

        if (ageInYears < AdultAgeYears)
        {
            return LifeStage.Child;
        }

        if (ageInYears < ElderAgeYears)
        {
            return LifeStage.Adult;
        }

        return LifeStage.Elder;
    }
}
