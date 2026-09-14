namespace ManyWinters.Godot.Logic;

// A span of ticks in the units the game counts time in: winters where there have been any, else
// seasons. A person's age reads by the same rule as the time since a band arrived, so both ask
// here rather than each rounding its own way.
internal static class DurationText
{
    internal static string For(long elapsedTicks, long ticksPerYear, long ticksPerSeason)
    {
        var winters = elapsedTicks / ticksPerYear;
        if (winters >= 1)
        {
            return $"{winters} winter{(winters == 1 ? "" : "s")}";
        }

        var seasons = elapsedTicks / ticksPerSeason;
        return $"{seasons} season{(seasons == 1 ? "" : "s")}";
    }
}
