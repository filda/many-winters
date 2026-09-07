using System.Globalization;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

public class BuildTagTests
{
    [Fact]
    public void TheTagIsTheBuildTimeToTheSecond()
    {
        // Asserted exactly: two builds a minute apart have to read as different tags, which is
        // the entire job (the previous hand-bumped tag failed at precisely this).
        var tag = BuildTag.For(new DateTimeOffset(2026, 9, 8, 14, 22, 7, TimeSpan.Zero));

        Assert.Equal("2026-09-08 14:22:07Z", tag);
    }

    [Fact]
    public void ABuildTimeFromAnotherTimezoneIsReportedInUtc()
    {
        // Whoever reads the log back is rarely on the machine that produced it - an offset the
        // reader has to apply in their head is an offset they will get wrong.
        var tag = BuildTag.For(new DateTimeOffset(2026, 9, 8, 16, 22, 7, TimeSpan.FromHours(2)));

        Assert.Equal("2026-09-08 14:22:07Z", tag);
    }

    [Fact]
    public void TheTagReadsTheSameWhicheverCultureTheMachineRunsIn()
    {
        // A machine on a non-Gregorian calendar would otherwise print a date nothing else in
        // the log or the repository agrees with.
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
        // A single-file publish reports no assembly location to stat. Printing today's date
        // there would answer the question wrongly instead of admitting it can't.
        Assert.Equal("unknown", BuildTag.For(null));
    }
}
