using System.Text;

namespace ManyWinters.Core.Population.Naming;

// Draws a name from a CultureProfile (or, for a founding generation with no history yet, from a
// flat global one) with light parent inheritance folded in as a probability nudge rather than
// string concatenation - see docs/Procedural Name Generation Plan.md, sections 1, 3 and 4.
public static class PhoneticNameGenerator
{
    // Candidates drawn per name before settling for the best of them; the profile's own weighting
    // already does most of the work, so this only has to be enough to dodge a rare duplicate or
    // an unlucky sibling clash.
    private const int CandidateCount = 8;

    // The plan's 50/30/20 split between long-term culture, recent trend and either parent.
    private const float CultureWeight = 0.5f;
    private const float TrendWeight = 0.3f;
    private const float ParentWeight = 0.2f;

    private static readonly CultureProfile GlobalProfile = BuildGlobalProfile();

    // Step 1 of the plan: the founding generation has no naming history to draw on, so it comes
    // from flat, unweighted phoneme frequencies instead of any CultureProfile.
    public static string GenerateFounding(Random rng, IReadOnlySet<string> existingNames) =>
        GenerateFrom(rng, GlobalProfile, existingNames, siblingNames: []);

    public static string GenerateChild(
        Random rng,
        CultureProfile culture,
        CultureProfile trend,
        string? motherName,
        string? fatherName,
        IReadOnlySet<string> existingNames,
        IReadOnlyList<string> siblingNames)
    {
        var blended = BlendForChild(culture, trend, motherName, fatherName);
        return GenerateFrom(rng, blended, existingNames, siblingNames);
    }

    private static CultureProfile BuildGlobalProfile()
    {
        var profile = new CultureProfile();
        foreach (var consonant in Phonotactics.Consonants)
        {
            profile.Onsets.Add(consonant, 1f);
            profile.Codas.Add(consonant, 1f);
        }

        foreach (var cluster in Phonotactics.Clusters)
        {
            profile.Onsets.Add(cluster, 1f);
        }

        // Vowel-initial names and open syllables (no coda) are both common enough to weigh in
        // alongside every consonant option, not just be possible as an edge case.
        profile.Onsets.Add(string.Empty, Phonotactics.Consonants.Length);
        profile.Codas.Add(string.Empty, Phonotactics.Consonants.Length * 2);

        foreach (var vowel in Phonotactics.Vowels)
        {
            profile.Nuclei.Add(vowel, 1f);
        }

        profile.SyllableCounts.Add(1, 1f);
        profile.SyllableCounts.Add(2, 3f);
        profile.SyllableCounts.Add(3, 1f);
        return profile;
    }

    private static CultureProfile BlendForChild(CultureProfile culture, CultureProfile trend, string? motherName, string? fatherName)
    {
        var blended = new CultureProfile();
        Merge(blended.Onsets, culture.Onsets, CultureWeight);
        Merge(blended.Onsets, trend.Onsets, TrendWeight);
        Merge(blended.Nuclei, culture.Nuclei, CultureWeight);
        Merge(blended.Nuclei, trend.Nuclei, TrendWeight);
        Merge(blended.Codas, culture.Codas, CultureWeight);
        Merge(blended.Codas, trend.Codas, TrendWeight);
        Merge(blended.SyllableCounts, culture.SyllableCounts, CultureWeight);
        Merge(blended.SyllableCounts, trend.SyllableCounts, TrendWeight);

        // A parent's own syllable features nudge the draw without ever being concatenated into
        // it directly (the plan's "Bran + Mira -> Bramira" is explicitly what this is not).
        foreach (var parentName in new[] { motherName, fatherName })
        {
            if (parentName is null)
            {
                continue;
            }

            foreach (var syllable in NameSyllables.Split(parentName))
            {
                blended.Onsets.Add(syllable.Onset, ParentWeight);
                blended.Nuclei.Add(syllable.Nucleus, ParentWeight);
                blended.Codas.Add(syllable.Coda, ParentWeight);
            }
        }

        if (blended.SyllableCounts.IsEmpty)
        {
            Merge(blended.SyllableCounts, GlobalProfile.SyllableCounts, 1f);
        }

        return blended;
    }

    private static void Merge<T>(WeightedSet<T> into, WeightedSet<T> from, float scale)
        where T : notnull
    {
        if (from.TotalWeight <= 0f)
        {
            return;
        }

        foreach (var (item, weight) in from.Enumerate())
        {
            into.Add(item, weight / from.TotalWeight * scale);
        }
    }

    private static string GenerateFrom(Random rng, CultureProfile profile, IReadOnlySet<string> existingNames, IReadOnlyList<string> siblingNames)
    {
        string? best = null;
        var bestScore = float.NegativeInfinity;

        for (var attempt = 0; attempt < CandidateCount; attempt++)
        {
            var candidate = DrawCandidate(rng, profile);
            if (candidate is null || existingNames.Contains(candidate))
            {
                continue;
            }

            var score = Score(candidate, siblingNames);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best ?? FallbackName(rng, existingNames);
    }

    private static string? DrawCandidate(Random rng, CultureProfile profile)
    {
        if (profile.Nuclei.IsEmpty)
        {
            return null;
        }

        var syllableCount = profile.SyllableCounts.IsEmpty
            ? 2
            : Math.Clamp(profile.SyllableCounts.Sample(rng), Phonotactics.MinSyllables, Phonotactics.MaxSyllables);

        var builder = new StringBuilder();
        for (var i = 0; i < syllableCount; i++)
        {
            var onset = profile.Onsets.IsEmpty ? string.Empty : profile.Onsets.Sample(rng);
            var nucleus = profile.Nuclei.Sample(rng);

            // Only the last syllable gets a coda: a mid-word consonant run belongs to the next
            // syllable's onset under maximal-onset segmentation (see NameSyllables.Split).
            var isLast = i == syllableCount - 1;
            var coda = isLast && !profile.Codas.IsEmpty ? profile.Codas.Sample(rng) : string.Empty;
            builder.Append(onset).Append(nucleus).Append(coda);
        }

        if (builder.Length == 0)
        {
            return null;
        }

        var name = char.ToUpperInvariant(builder[0]) + builder.ToString(1, builder.Length - 1);
        return Phonotactics.IsPronounceable(name) ? name : null;
    }

    // + cultural similarity is already baked into the weighted draw; this only has to catch what
    // the profile can't see on its own: a name too close to a sibling's.
    private static float Score(string candidate, IReadOnlyList<string> siblingNames)
    {
        var score = 0f;
        var lower = candidate.ToLowerInvariant();

        foreach (var sibling in siblingNames)
        {
            var siblingLower = sibling.ToLowerInvariant();
            if (siblingLower == lower)
            {
                score -= 100f;
            }
            else if (siblingLower.Length >= 3 && lower.Length >= 3 && siblingLower[..3] == lower[..3])
            {
                score -= 5f;
            }
        }

        return score;
    }

    // Reached only if every candidate above collided with an existing name or a sibling outright
    // - rare enough that a plain CVC draw with no phonotactic retry is a fine last resort.
    private static string FallbackName(Random rng, IReadOnlySet<string> existingNames)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var onset = Phonotactics.Consonants[rng.Next(Phonotactics.Consonants.Length)];
            var nucleus = Phonotactics.Vowels[rng.Next(Phonotactics.Vowels.Length)];
            var coda = Phonotactics.Consonants[rng.Next(Phonotactics.Consonants.Length)];
            var name = char.ToUpperInvariant(onset[0]) + nucleus + coda;
            if (!existingNames.Contains(name))
            {
                return name;
            }
        }

        return $"Kin{rng.Next(1000)}";
    }
}
