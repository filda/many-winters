namespace ManyWinters.Core.World;

// How the abstract calendar (Season) maps onto the climate that actually drives gameplay
// (hunger cost, resource yield/regrowth). Swapping which season is Cold vs. Hot - e.g. for
// a southern-hemisphere scenario - is a different SeasonParameters instance, not a code change.
public sealed class SeasonParameters(
    IReadOnlyDictionary<Season, Climate> climateBySeason,
    IReadOnlyDictionary<Climate, float> hungerMultiplierByClimate,
    IReadOnlyDictionary<Climate, float> regenMultiplierByClimate)
{
    public static SeasonParameters Default { get; } = new(
        climateBySeason: new Dictionary<Season, Climate>
        {
            [Season.Spring] = Climate.Mild,
            [Season.Summer] = Climate.Hot,
            [Season.Autumn] = Climate.Mild,
            [Season.Winter] = Climate.Cold,
        },
        hungerMultiplierByClimate: new Dictionary<Climate, float>
        {
            [Climate.Cold] = 2f,
            [Climate.Mild] = 1f,
            [Climate.Hot] = 1f,
        },
        regenMultiplierByClimate: new Dictionary<Climate, float>
        {
            [Climate.Cold] = 0f,
            [Climate.Mild] = 1f,
            [Climate.Hot] = 1f,
        });

    public Climate ClimateFor(Season season) => climateBySeason.GetValueOrDefault(season, Climate.Mild);

    public float HungerMultiplierFor(Climate climate) => hungerMultiplierByClimate.GetValueOrDefault(climate, 1f);

    public float RegenMultiplierFor(Climate climate) => regenMultiplierByClimate.GetValueOrDefault(climate, 1f);
}
