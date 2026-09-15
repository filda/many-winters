using ManyWinters.Core.Population.Naming;

namespace ManyWinters.Tests.Population.Naming;

public class NameSyllablesTests
{
    [Fact]
    public void AVowelInitialOpenSyllableHasNoOnsetOrCoda()
    {
        var syllables = NameSyllables.Split("A");

        Assert.Equal([new Syllable("", "a", "")], syllables);
    }

    [Fact]
    public void ASingleClosedSyllableKeepsItsCoda()
    {
        var syllables = NameSyllables.Split("Bran");

        Assert.Equal([new Syllable("br", "a", "n")], syllables);
    }

    [Fact]
    public void AConsonantRunBetweenTwoVowelsBecomesTheNextSyllablesOnsetNotThePreviousOnesCoda()
    {
        // Ava: A | v | a - the "v" is Ava's second syllable's onset, not the first's coda.
        var syllables = NameSyllables.Split("Ava");

        Assert.Equal([new Syllable("", "a", ""), new Syllable("v", "a", "")], syllables);
    }

    [Fact]
    public void OnlyTheFinalConsonantRunIsACoda()
    {
        // Vessa: v | e | ss | a - "ss" sits between two vowels, so it opens the second syllable
        // rather than closing the first.
        var syllables = NameSyllables.Split("Vessa");

        Assert.Equal([new Syllable("v", "e", ""), new Syllable("ss", "a", "")], syllables);
    }

    [Fact]
    public void SplittingIsCaseInsensitive()
    {
        Assert.Equal(NameSyllables.Split("bran"), NameSyllables.Split("BRAN"));
    }

    [Fact]
    public void AWordWithNoVowelsHasNoSyllables()
    {
        Assert.Empty(NameSyllables.Split("Brr"));
    }
}
