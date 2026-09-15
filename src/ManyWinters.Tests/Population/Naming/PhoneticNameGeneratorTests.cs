using ManyWinters.Core.Population.Naming;

namespace ManyWinters.Tests.Population.Naming;

public class PhoneticNameGeneratorTests
{
    [Fact]
    public void GenerateFoundingProducesAPronounceableCapitalizedName()
    {
        var name = PhoneticNameGenerator.GenerateFounding(new Random(1), new HashSet<string>());

        Assert.True(Phonotactics.IsPronounceable(name));
        Assert.Equal(char.ToUpperInvariant(name[0]), name[0]);
    }

    [Fact]
    public void GenerateFoundingIsDeterministicForTheSameSeed()
    {
        var first = PhoneticNameGenerator.GenerateFounding(new Random(123), new HashSet<string>());
        var second = PhoneticNameGenerator.GenerateFounding(new Random(123), new HashSet<string>());

        Assert.Equal(first, second);
    }

    [Fact]
    public void GenerateFoundingNeverRepeatsAnExistingName()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rng = new Random(2);

        for (var i = 0; i < 200; i++)
        {
            var name = PhoneticNameGenerator.GenerateFounding(rng, existing);
            Assert.DoesNotContain(name, existing);
            existing.Add(name);
        }
    }

    [Fact]
    public void GenerateChildDrawsFromTheCultureRatherThanConcatenatingTheParentsNames()
    {
        var culture = CultureProfile.Build(["Bran", "Doran", "Mira", "Tora"], decayPerObservation: 0.98f);
        var trend = CultureProfile.BuildRecentTrend(["Bran", "Doran", "Mira", "Tora"], windowSize: 4);
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Bran", "Doran", "Mira", "Tora" };
        var rng = new Random(9);

        for (var i = 0; i < 50; i++)
        {
            var name = PhoneticNameGenerator.GenerateChild(rng, culture, trend, "Bran", "Mira", existing, siblingNames: []);

            Assert.True(Phonotactics.IsPronounceable(name));
            Assert.NotEqual("Bramira", name, StringComparer.OrdinalIgnoreCase);
            existing.Add(name);
        }
    }

    [Fact]
    public void GenerateChildNeverReturnsAnExistingNameEvenWithSiblingsPresent()
    {
        var culture = CultureProfile.Build(["Bran", "Doran"], decayPerObservation: 0.98f);
        var trend = CultureProfile.BuildRecentTrend(["Bran", "Doran"], windowSize: 2);
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Bran", "Doran" };
        var rng = new Random(4);

        for (var i = 0; i < 50; i++)
        {
            var name = PhoneticNameGenerator.GenerateChild(rng, culture, trend, "Bran", "Doran", existing, siblingNames: ["Doran"]);
            Assert.DoesNotContain(name, existing);
            existing.Add(name);
        }
    }
}
