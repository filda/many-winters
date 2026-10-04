using ManyWinters.Core.Population;

namespace ManyWinters.Presentation.Logic;

// How big a creature is drawn for its age, as a fraction of its grown size. Linear over the
// whole of childhood rather than stepped by life stage: a stage lasts years, so stage steps
// would draw a three-year-old the same as a one-year-old and then jump at four.
internal static class Stature
{
    // Not the real newborn-to-adult ratio, which would leave a dot too small to click on.
    internal const double NewbornScale = 0.45;

    internal static double ScaleFor(double ageInYears, LifeCycle lifeCycle) =>
        NewbornScale + ((1 - NewbornScale) * Math.Min(1, ageInYears / Math.Max(1, lifeCycle.AdultAgeYears)));

    // The stage the simulation would name at this age, from the whole winters lived so far: the
    // one place a view and a portrait both ask, so the child on the card is the child in the world.
    internal static LifeStage StageAt(double ageInYears, LifeCycle lifeCycle) =>
        lifeCycle.StageFor((long)Math.Floor(ageInYears));
}
