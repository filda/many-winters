using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// Per-instance variety keyed off an entity's stable id. The values are behaviour, not
// implementation detail: a saved world has to look the same when reloaded, so regenerate these
// deliberately if the variation is meant to change rather than relaxing them.
public class EntityVisualVariationTests
{
    [Fact]
    public void TintShiftsHueAndBrightnessByAFixedAmountForAGivenSeed()
    {
        var tinted = EntityVisualVariation.Tint(new Color(0.5f, 0.4f, 0.25f), seed: 42);

        Assert.Equal(0.39227217f, tinted.R, 5);
        Assert.Equal(0.32964417f, tinted.G, 5);
        Assert.Equal(0.19613609f, tinted.B, 5);
    }

    [Fact]
    public void TintLeavesSaturationAndAlphaAlone()
    {
        // Only hue and brightness are varied - touching saturation would drift a kind's colour
        // identity, and touching alpha would fight the occlusion fade.
        var baseColor = Color.FromHsv(0.3f, 0.6f, 0.5f, 0.75f);

        var tinted = EntityVisualVariation.Tint(baseColor, seed: 42);

        Assert.Equal(baseColor.S, tinted.S, 5);
        Assert.Equal(baseColor.A, tinted.A, 5);
    }

    [Fact]
    public void AHueShiftedPastTheEndOfTheWheelWrapsAroundInsteadOfSaturating()
    {
        // Base hue 0.995 plus this seed's positive shift lands past 1. Hue is a wheel, so it
        // has to come back round to just above 0 - clamping instead would pin every red-ish
        // entity to the exact same hue.
        var tinted = EntityVisualVariation.Tint(Color.FromHsv(0.995f, 0.6f, 0.5f), seed: 42);

        Assert.Equal(0.0084486f, tinted.H, 5);
    }

    [Fact]
    public void BrightnessIsClampedRatherThanAllowedOutOfRange()
    {
        // An already-black base with this seed's negative shift would go below zero.
        var tinted = EntityVisualVariation.Tint(Color.FromHsv(0.3f, 0.6f, 0f), seed: 42);

        Assert.Equal(0f, tinted.V, 5);
    }

    [Fact]
    public void TheSameSeedAlwaysGivesTheSameTintAndDifferentSeedsDoNot()
    {
        var baseColor = new Color(0.5f, 0.4f, 0.25f);

        Assert.Equal(EntityVisualVariation.Tint(baseColor, 42), EntityVisualVariation.Tint(baseColor, 42));
        Assert.NotEqual(EntityVisualVariation.Tint(baseColor, 42), EntityVisualVariation.Tint(baseColor, 43));
    }

    [Fact]
    public void ScaleLandsAtAFixedPointInsideTheGivenRange()
    {
        Assert.Equal(1.0672426f, EntityVisualVariation.Scale(42, 0.8f, 1.2f), 5);
    }

    [Fact]
    public void ScaleStaysInsideTheRangeForEverySeed()
    {
        for (var seed = -500; seed < 500; seed++)
        {
            Assert.InRange(EntityVisualVariation.Scale(seed, 0.8f, 1.2f), 0.8f, 1.2f);
        }
    }

    [Fact]
    public void ARangeWithNoWidthGivesExactlyThatValue()
    {
        // Degenerate but reachable from content: min and max equal means no variety at all,
        // not a value drifting off one end.
        Assert.Equal(0.9f, EntityVisualVariation.Scale(42, 0.9f, 0.9f), 5);
    }

    [Theory]
    [InlineData(1, 0.53804964f)]
    [InlineData(2, 0.79895407f)]
    [InlineData(3, 0.7406873f)]
    public void EachSaltDrawsItsOwnValueFromTheSameSeed(int salt, float expected)
    {
        // A person's walk rate, bob and rock all come off one id - without a per-attribute
        // salt they would be the same underlying draw, just rescaled, so the three would move
        // in lockstep.
        Assert.Equal(expected, EntityVisualVariation.RangeFor(42, salt, 0f, 1f), 5);
    }

    [Fact]
    public void AdjacentSeedsComeOutFarApartRatherThanNearlyIdentical()
    {
        // The whole reason for avalanching seed and salt first: System.Random on neighbouring
        // small seeds draws eerily similar first values, and two entity ids can land next to
        // each other. Without it, two trees spawned in a row would look like copies.
        var first = EntityVisualVariation.RangeFor(1, 0, 0f, 1f);
        var second = EntityVisualVariation.RangeFor(2, 0, 0f, 1f);

        Assert.True(Math.Abs(first - second) > 0.1f, $"{first} and {second} are too close");
    }

    [Fact]
    public void RangeForScalesTheDrawAcrossTheWidthAndOffsetsItByTheMinimum()
    {
        // Deliberately not 0..1, where the width is 1 and the offset 0 - there, multiplying by
        // the width, dividing by it, and using max + min instead all give the same answer, so
        // the arithmetic goes untested. Two to six is the same draw (0.538) placed at 4.152.
        Assert.Equal(4.1521986f, EntityVisualVariation.RangeFor(42, 1, 2f, 6f), 5);
    }

    [Fact]
    public void TheSameSeedAndSaltAlwaysDrawTheSameValue()
    {
        Assert.Equal(EntityVisualVariation.RangeFor(42, 1, 0f, 1f), EntityVisualVariation.RangeFor(42, 1, 0f, 1f));
    }

    [Theory]
    [InlineData(4, 2, 0)]
    [InlineData(5, 3, 1)]
    public void IndexForPicksAFixedOptionForAGivenSeedAndSalt(int salt, int count, int expected)
    {
        Assert.Equal(expected, EntityVisualVariation.IndexFor(42, salt, count));
    }

    [Fact]
    public void OneOptionIsAlwaysThatOption()
    {
        Assert.Equal(0, EntityVisualVariation.IndexFor(42, 4, 1));
    }

    [Fact]
    public void EveryOptionGetsPickedBySomeSeedAndNoneOutOfRange()
    {
        // A hairstyle or clothing option nobody ever draws is content that never appears in
        // the game, and one past the end is an index out of range on the array it selects from.
        var seen = new HashSet<int>();
        for (var seed = 0; seed < 500; seed++)
        {
            var index = EntityVisualVariation.IndexFor(seed, salt: 7, count: 4);
            Assert.InRange(index, 0, 3);
            seen.Add(index);
        }

        Assert.Equal(4, seen.Count);
    }
}
