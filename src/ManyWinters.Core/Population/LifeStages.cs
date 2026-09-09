namespace ManyWinters.Core.Population;

public static class LifeStages
{
    // Still nursing: too young to forage, to be taught anything it could act on, or to be
    // left behind (see WorldState.DecideIdleTask). Absolute winters rather than a fraction of
    // MaxLifespanYears - a world tuned to a three-winter lifespan for a test is not a world
    // where childhood lasts four months, it is a world nobody grows up in, and that is the
    // honest reading of such a rule set.
    public const long WeaningAgeYears = 1;

    // Grown: a full load on the back (CarryCapacity), and old enough for children of their own.
    public const long AdultAgeYears = 4;

    // Past their physical peak. Nothing is taken away - they simply carry a little less.
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
