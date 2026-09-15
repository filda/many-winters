namespace ManyWinters.Core.Population.Naming;

// Letter sets and shape rules a generated name has to satisfy - not a model of any real
// language, just enough structure to keep every draw pronounceable
// (docs/Procedural Name Generation Plan.md).
public static class Phonotactics
{
    public static readonly string[] Consonants =
        ["b", "c", "d", "f", "g", "h", "j", "k", "l", "m", "n", "p", "r", "s", "t", "v", "w", "y", "z"];

    public static readonly string[] Vowels = ["a", "e", "i", "o", "u"];

    // Onset clusters a syllable may open with, in addition to any single consonant.
    public static readonly string[] Clusters =
    [
        "br", "cr", "dr", "fr", "gr", "kr", "pr", "tr", "vr",
        "bl", "cl", "fl", "gl", "pl", "sl",
        "sk", "sm", "sn", "sp", "st", "sw", "tw",
    ];

    private const int MinNameLength = 3;
    private const int MaxNameLength = 9;
    public const int MinSyllables = 1;
    public const int MaxSyllables = 3;

    private static readonly HashSet<char> ConsonantChars = Consonants.Select(c => c[0]).ToHashSet();
    private static readonly HashSet<char> VowelChars = Vowels.Select(v => v[0]).ToHashSet();

    public static bool IsConsonant(char c) => ConsonantChars.Contains(char.ToLowerInvariant(c));

    public static bool IsVowel(char c) => VowelChars.Contains(char.ToLowerInvariant(c));

    // Rejects the shapes a generator can produce but a reader would stumble on: three letters of
    // the same class in a row, the same letter three times running, or a length outside the
    // band every other generated name keeps to.
    public static bool IsPronounceable(string name)
    {
        if (name.Length is < MinNameLength or > MaxNameLength)
        {
            return false;
        }

        var lower = name.ToLowerInvariant();
        var sameClassRun = 1;
        var sameLetterRun = 1;

        for (var i = 1; i < lower.Length; i++)
        {
            var sameClass = IsVowel(lower[i]) == IsVowel(lower[i - 1]);
            sameClassRun = sameClass ? sameClassRun + 1 : 1;
            if (sameClassRun > 2)
            {
                return false;
            }

            sameLetterRun = lower[i] == lower[i - 1] ? sameLetterRun + 1 : 1;
            if (sameLetterRun > 2)
            {
                return false;
            }
        }

        return true;
    }
}
