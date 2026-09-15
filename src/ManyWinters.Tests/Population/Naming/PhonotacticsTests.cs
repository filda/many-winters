using ManyWinters.Core.Population.Naming;

namespace ManyWinters.Tests.Population.Naming;

public class PhonotacticsTests
{
    [Theory]
    [InlineData("Ava")]
    [InlineData("Bran")]
    [InlineData("Corin")]
    [InlineData("Vessa")]
    public void AnOrdinaryNameIsPronounceable(string name)
    {
        Assert.True(Phonotactics.IsPronounceable(name));
    }

    [Theory]
    [InlineData("Ab")] // shorter than MinNameLength
    [InlineData("Abcdefghij")] // longer than MaxNameLength
    public void ANameOutsideTheLengthBandIsRejected(string name)
    {
        Assert.False(Phonotactics.IsPronounceable(name));
    }

    [Theory]
    [InlineData("Strgna")] // four consonants in a row
    [InlineData("Aoeiu")] // five vowels in a row
    public void ThreeLettersOfTheSameClassInARowIsRejected(string name)
    {
        Assert.False(Phonotactics.IsPronounceable(name));
    }

    [Fact]
    public void TheSameLetterThreeTimesRunningIsRejected()
    {
        Assert.False(Phonotactics.IsPronounceable("Baaal"));
    }

    [Fact]
    public void TheSameLetterTwiceRunningIsAllowed()
    {
        Assert.True(Phonotactics.IsPronounceable("Anna"));
    }

    [Theory]
    [InlineData('b', false)]
    [InlineData('a', true)]
    public void IsConsonantAgreesWithIsVowel(char letter, bool isVowel)
    {
        Assert.Equal(isVowel, Phonotactics.IsVowel(letter));
        Assert.Equal(!isVowel, Phonotactics.IsConsonant(letter));
    }
}
