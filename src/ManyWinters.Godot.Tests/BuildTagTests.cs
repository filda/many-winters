using System.Globalization;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

public class BuildTagTests
{
    [Fact]
    public void TheTagIsTheBuildTimeToTheSecond()
    {
        // Asserted exactly: two builds a minute apart have to read as different tags.
        var tag = BuildTag.For(new DateTimeOffset(2026, 9, 8, 14, 22, 7, TimeSpan.Zero));

        Assert.Equal("2026-09-08 14:22:07Z", tag);
    }

    [Fact]
    public void ABuildTimeFromAnotherTimezoneIsReportedInUtc()
    {
        // The reader is rarely on the machine that produced the log; an offset applied in the
        // head is one they will get wrong.
        var tag = BuildTag.For(new DateTimeOffset(2026, 9, 8, 16, 22, 7, TimeSpan.FromHours(2)));

        Assert.Equal("2026-09-08 14:22:07Z", tag);
    }

    [Fact]
    public void TheTagReadsTheSameWhicheverCultureTheMachineRunsIn()
    {
        // A machine on a non-Gregorian calendar would otherwise print a date nothing else
        // agrees with.
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");

            Assert.Equal("2026-09-08 14:22:07Z", BuildTag.For(new DateTimeOffset(2026, 9, 8, 14, 22, 7, TimeSpan.Zero)));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void NoBuildTimeAtAllSaysSoRatherThanInventingOne()
    {
        // A single-file publish has no assembly location to stat; today's date would be a wrong
        // answer rather than an honest one.
        Assert.Equal("unknown", BuildTag.For(null));
    }
}
