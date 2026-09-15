namespace ManyWinters.Core.Population.Naming;

// The accumulated memory of how a population has named its people - built by replaying the
// names it has actually given, not maintained as generator state (docs/Procedural Name
// Generation Plan.md, "Design Principle"). Every field is a frequency table over what people
// were really called; nothing here is an abstract knob like "harshness".
public sealed class CultureProfile
{
    public WeightedSet<string> Onsets { get; } = new();

    public WeightedSet<string> Nuclei { get; } = new();

    public WeightedSet<string> Codas { get; } = new();

    public WeightedSet<int> SyllableCounts { get; } = new();

    public void Observe(string name)
    {
        var syllables = NameSyllables.Split(name);
        if (syllables.Count == 0)
        {
            return;
        }

        SyllableCounts.Add(syllables.Count, 1f);
        foreach (var syllable in syllables)
        {
            Onsets.Add(syllable.Onset, 1f);
            Nuclei.Add(syllable.Nucleus, 1f);
            Codas.Add(syllable.Coda, 1f);
        }
    }

    public void Decay(float factor)
    {
        Onsets.DecayAll(factor);
        Nuclei.DecayAll(factor);
        Codas.DecayAll(factor);
        SyllableCounts.DecayAll(factor);
    }

    // Builds a profile from a population's whole naming history, oldest name first, decaying
    // between observations so recent names outweigh ancient ones without discarding them - the
    // plan's `profile *= 0.95f; profile.Observe(newborn.Name)` loop, replayed from scratch
    // instead of carried as save-file state: every name it needs already lives in
    // WorldState.People/Forebears, which the save file already has (see WorldState.NamingHistory).
    public static CultureProfile Build(IEnumerable<string> namesOldestFirst, float decayPerObservation)
    {
        var profile = new CultureProfile();
        foreach (var name in namesOldestFirst)
        {
            profile.Decay(decayPerObservation);
            profile.Observe(name);
        }

        return profile;
    }

    // A short-term companion to Build's long-term profile: only the most recent names, undecayed
    // among themselves, so a run of similar-sounding births can nudge the next child without the
    // long-term tradition it hasn't yet become (the plan's `NamingTrend`).
    public static CultureProfile BuildRecentTrend(IReadOnlyList<string> namesOldestFirst, int windowSize) =>
        Build(namesOldestFirst.TakeLast(windowSize), decayPerObservation: 1f);
}
