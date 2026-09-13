using System.Globalization;

namespace ManyWinters.Core.Continuity;

// "Nine winters", not "9 winters": a chronicle is written out in words. Up to ninety-nine,
// which covers any count of winters, children or graves a band realistically leaves behind;
// past that the digits are the honest choice over a paragraph of hundreds and thousands.
public static class NumberWords
{
    private static readonly string[] Units =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
        "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen",
    ];

    private static readonly string[] Tens =
    [
        "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety",
    ];

    public static string Of(int value)
    {
        if (value < 0 || value >= 100)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        if (value < Units.Length)
        {
            return Units[value];
        }

        var tens = Tens[(value / 10) - 2];
        var unit = value % 10;
        return unit == 0 ? tens : $"{tens}-{Units[unit]}";
    }
}
