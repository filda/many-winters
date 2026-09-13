using ManyWinters.Core.Continuity;

namespace ManyWinters.Tests.Continuity;

public class NumberWordsTests
{
    [Theory]
    [InlineData(0, "zero")]
    [InlineData(1, "one")]
    [InlineData(9, "nine")]
    [InlineData(10, "ten")]
    [InlineData(13, "thirteen")]
    [InlineData(19, "nineteen")]
    [InlineData(20, "twenty")]
    [InlineData(21, "twenty-one")]
    [InlineData(45, "forty-five")]
    [InlineData(90, "ninety")]
    [InlineData(99, "ninety-nine")]
    public void SmallCountsAreWrittenOutInWords(int value, string expected)
    {
        Assert.Equal(expected, NumberWords.Of(value));
    }

    [Theory]
    [InlineData(2, "two")]
    [InlineData(3, "three")]
    [InlineData(4, "four")]
    [InlineData(5, "five")]
    [InlineData(6, "six")]
    [InlineData(7, "seven")]
    [InlineData(8, "eight")]
    [InlineData(11, "eleven")]
    [InlineData(12, "twelve")]
    [InlineData(14, "fourteen")]
    [InlineData(15, "fifteen")]
    [InlineData(16, "sixteen")]
    [InlineData(17, "seventeen")]
    [InlineData(18, "eighteen")]
    [InlineData(30, "thirty")]
    [InlineData(50, "fifty")]
    [InlineData(60, "sixty")]
    [InlineData(70, "seventy")]
    [InlineData(80, "eighty")]
    public void EveryWordInTheTablesIsTheRightOne(int value, string expected)
    {
        // Each entry is content carved on an inscription, so each one is asserted, not only the
        // tables' shape.
        Assert.Equal(expected, NumberWords.Of(value));
    }

    [Theory]
    [InlineData(100, "100")]
    [InlineData(1234, "1234")]
    [InlineData(-1, "-1")]
    public void CountsOutsideTheTablesFallBackToDigits(int value, string expected)
    {
        Assert.Equal(expected, NumberWords.Of(value));
    }
}
