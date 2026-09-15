using ManyWinters.Core.Population.Naming;

namespace ManyWinters.Tests.Population.Naming;

public class CultureProfileTests
{
    [Fact]
    public void ObserveRecordsEverySyllablesOnsetNucleusCodaAndTheSyllableCount()
    {
        var profile = new CultureProfile();

        profile.Observe("Bran");

        Assert.Equal(("br", 1f), profile.Onsets.Enumerate().Single());
        Assert.Equal(("a", 1f), profile.Nuclei.Enumerate().Single());
        Assert.Equal(("n", 1f), profile.Codas.Enumerate().Single());
        Assert.Equal((1, 1f), profile.SyllableCounts.Enumerate().Single());
    }

    [Fact]
    public void ObservingANameWithNoVowelsDoesNothing()
    {
        var profile = new CultureProfile();

        profile.Observe("Brr");

        Assert.True(profile.Onsets.IsEmpty);
        Assert.True(profile.SyllableCounts.IsEmpty);
    }

    [Fact]
    public void DecayShrinksEveryFeatureBySameFactor()
    {
        var profile = new CultureProfile();
        profile.Observe("Bran");

        profile.Decay(0.5f);

        Assert.Equal(0.5f, profile.Onsets.TotalWeight);
        Assert.Equal(0.5f, profile.SyllableCounts.TotalWeight);
    }

    [Fact]
    public void BuildAppliesDecayBeforeEachObservationSoOlderNamesEndUpLighter()
    {
        // Neither name shares an onset with the other, so each one's surviving weight is exactly
        // its own decay history: "Bran" (oldest) is decayed once, by the pass before "Doran" is
        // observed; "Doran" (newest) is never decayed at all.
        var profile = CultureProfile.Build(["Bran", "Doran"], decayPerObservation: 0.5f);

        var onsetWeights = profile.Onsets.Enumerate().ToDictionary(e => e.Item, e => e.Weight);
        Assert.Equal(0.5f, onsetWeights["br"]);
        Assert.Equal(1f, onsetWeights["d"]);
    }

    [Fact]
    public void BuildRecentTrendOnlyLooksAtTheLastNamesInTheWindow()
    {
        var trend = CultureProfile.BuildRecentTrend(["Ava", "Bran", "Doran"], windowSize: 2);

        var onsets = trend.Onsets.Enumerate().Select(e => e.Item).ToList();
        Assert.DoesNotContain(string.Empty, onsets); // "Ava" (outside the window) opens with no onset
        Assert.Contains("br", onsets);
        Assert.Contains("d", onsets);
    }
}
