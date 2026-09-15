namespace ManyWinters.Core.Population.Naming;

// One syllable's onset (leading consonants), nucleus (vowel run) and coda (trailing consonants
// before the next syllable, or the word's tail) - the three slots CultureProfile keeps a
// WeightedSet for.
public readonly record struct Syllable(string Onset, string Nucleus, string Coda);

public static class NameSyllables
{
    // Maximal-onset segmentation: a consonant run between two vowel runs belongs to the onset of
    // the following syllable, so only a run at the very end of the word can be a coda. Works on
    // any lowercase-able alphabetic string, generated or curated alike, so a founding name and a
    // name this generator draws feed CultureProfile the same way.
    public static IReadOnlyList<Syllable> Split(string name)
    {
        var runs = SplitIntoRuns(name.ToLowerInvariant());
        var syllables = new List<Syllable>();

        for (var i = 0; i < runs.Count; i++)
        {
            if (!runs[i].IsVowel)
            {
                continue;
            }

            var onset = i > 0 && !runs[i - 1].IsVowel ? runs[i - 1].Text : string.Empty;
            var isLastVowelRun = !runs.Skip(i + 1).Any(run => run.IsVowel);
            var coda = isLastVowelRun && i + 1 < runs.Count && !runs[i + 1].IsVowel ? runs[i + 1].Text : string.Empty;
            syllables.Add(new Syllable(onset, runs[i].Text, coda));
        }

        return syllables;
    }

    private static List<(bool IsVowel, string Text)> SplitIntoRuns(string lower)
    {
        var runs = new List<(bool IsVowel, string Text)>();
        var start = 0;
        for (var i = 1; i <= lower.Length; i++)
        {
            if (i == lower.Length || Phonotactics.IsVowel(lower[i]) != Phonotactics.IsVowel(lower[start]))
            {
                runs.Add((Phonotactics.IsVowel(lower[start]), lower[start..i]));
                start = i;
            }
        }

        return runs;
    }
}
