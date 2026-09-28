using ManyWinters.Core.World;

namespace ManyWinters.Core.Population.Naming;

public sealed class NamingCulture(
    IReadOnlyList<Person> people,
    IReadOnlyList<Person> forebears,
    float CultureDecayPerObservation,
    int RecentTrendWindow,
    float CultureWeight,
    float TrendWeight,
    float ParentWeight
    )
{
    private int _namingHistoryVersion = -1;
    private CultureProfile? _cachedCultureProfile;
    private CultureProfile? _cachedTrendProfile;
    private HashSet<string>? _cachedExistingNames;

    // Deterministic from the parents and the tick, so a replayed world names the same children.
    // The culture it draws from is rebuilt from People/Forebears rather than saved separately.
    //
    // Public because a child the player asks for is named the same way as one the band has of
    // its own accord: BirthCommand takes the name, so somebody has to draw it, and there is only
    // one right way to draw it.
    public string NameForNewborn(Person mother, Person father, long tick)
    {
        var (culture, trend, existingNames) = NamingProfiles();
        var siblingNames = people
            .Where(person => ReferenceEquals(person.Mother, mother) && ReferenceEquals(person.Father, father))
            .Select(person => person.Name)
            .ToList();

        var mixed = unchecked((uint)(mother.Id.Seed * 73856093) ^ (uint)(father.Id.Seed * 19349663) ^ ((uint)tick * 2654435761u));
        var rng = new Random(SeedHash.Avalanche(mixed));

        return PhoneticNameGenerator.GenerateChild(rng, culture, trend, mother.Name, father.Name, existingNames, siblingNames, CultureWeight, TrendWeight, ParentWeight);
    }

    // A name for someone with no parents to inherit from, drawn from the current naming culture
    // rather than a curated pool - what the player's manual "Spawn Person" button uses.
    public string GenerateUnrelatedName(Random rng)
    {
        var (culture, trend, existingNames) = NamingProfiles();
        return PhoneticNameGenerator.GenerateChild(rng, culture, trend, motherName: null, fatherName: null, existingNames, siblingNames: [], CultureWeight, TrendWeight, ParentWeight);
    }

    // Cached against the total number of people and forebears (both only ever grow): rebuilding
    // the whole profile from history is cheap once, but the family-starting pass calls
    // NameForNewborn speculatively for every eligible pair on every tick, and only some of those
    // become an actual birth.
    private (CultureProfile Culture, CultureProfile Trend, HashSet<string> ExistingNames) NamingProfiles()
    {
        var version = people.Count + forebears.Count;
        if (version != _namingHistoryVersion || _cachedCultureProfile is null || _cachedTrendProfile is null || _cachedExistingNames is null)
        {
            var history = forebears.Concat(people).OrderBy(person => person.BirthTick).Select(person => person.Name).ToList();
            _cachedCultureProfile = CultureProfile.Build(history, CultureDecayPerObservation);
            _cachedTrendProfile = CultureProfile.BuildRecentTrend(history, RecentTrendWindow);
            _cachedExistingNames = new HashSet<string>(history, StringComparer.OrdinalIgnoreCase);
            _namingHistoryVersion = version;
        }

        return (_cachedCultureProfile, _cachedTrendProfile, _cachedExistingNames);
    }
}
