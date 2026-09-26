namespace ManyWinters.Core.Population;

// One species' age bands and lifespan (docs/todo/fauna-plan.md, step 0c: "clovek je taky druh" -
// a human is a species too). Today the only LifeCycle in the game is the human one, and its
// numbers are exactly what LifeStages and SimulationRules.MaxLifespanYears used to hardcode -
// nothing about behaviour changes yet. An animal's own LifeCycle is a second value for these same
// four fields, not a second shape: diet, herd, gestation, speed and collision radius are not
// here because no second value for any of them exists yet either (they arrive with the step that
// needs them).
public sealed record LifeCycle(
    // Still nursing: too young to forage, be taught, or be left behind (WorldState.DecideIdleTask).
    // Absolute winters, not a fraction of MaxLifespanYears: a species with a three-winter
    // lifespan is one nobody grows up in, not one where childhood lasts four months.
    long WeaningAgeYears,
    // Grown: a full load on the back (CarryCapacity), and old enough for children of their own.
    long AdultAgeYears,
    // Past their physical peak; they simply carry a little less.
    long ElderAgeYears,
    // Nobody outlives this, whatever else keeps them fed (WorldState.Advance's old-age check).
    long MaxLifespanYears)
{
    public LifeStage StageFor(long ageInYears)
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
