using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class ImpactModelTests
{
    private const int SampleRate = 22050;

    // Shipped materials from the plan: stone is hard and dense but brittle (low toughness), wood
    // is soft, light and tough.
    private static readonly ImpactMaterial Stone = new(Hardness: 1.0f, Toughness: 0.15f, Density: 2.0f);
    private static readonly ImpactMaterial Wood = new(Hardness: 0.4f, Toughness: 0.7f, Density: 0.5f);

    [Fact]
    public void SameSeedRendersAnIdenticalBuffer()
    {
        var first = ImpactModel.Render(Stone, Wood, 1.0f, SampleRate, 613);
        var second = ImpactModel.Render(Stone, Wood, 1.0f, SampleRate, 613);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentSeedsRenderDifferentBuffers()
    {
        var first = ImpactModel.Render(Stone, Wood, 1.0f, SampleRate, 613);
        var second = ImpactModel.Render(Stone, Wood, 1.0f, SampleRate, 614);

        Assert.NotEqual(first, second);
    }

    // Every render is normalised to the same peak, so nothing can clip and the blind gate cannot
    // be graded on loudness. Asserting the exact target rather than "at most 1" is what keeps a
    // broken normalisation from passing.
    [Theory]
    [InlineData(1.0f, 0.15f, 2.0f, 0.4f, 0.7f, 0.5f)]
    [InlineData(1.0f, 0.15f, 2.0f, 1.0f, 0.15f, 2.0f)]
    [InlineData(0.4f, 0.7f, 0.5f, 0.4f, 0.7f, 0.5f)]
    public void EveryRenderIsNormalisedToTheSamePeak(
        float strikerHardness, float strikerToughness, float strikerDensity,
        float struckHardness, float struckToughness, float struckDensity)
    {
        var samples = ImpactModel.Render(
            new ImpactMaterial(strikerHardness, strikerToughness, strikerDensity),
            new ImpactMaterial(struckHardness, struckToughness, struckDensity),
            1.0f,
            SampleRate,
            42);

        Assert.Equal(0.9f, Analysis.Peak(samples), 4);
    }

    [Fact]
    public void StoneOnStoneHasAHigherSpectralCentroidThanWoodOnWood()
    {
        var stoneOnStone = ImpactModel.Render(Stone, Stone, 1.0f, SampleRate, 7);
        var woodOnWood = ImpactModel.Render(Wood, Wood, 1.0f, SampleRate, 7);

        Assert.True(
            Analysis.SpectralCentroid(stoneOnStone, SampleRate) > Analysis.SpectralCentroid(woodOnWood, SampleRate));
    }

    // The plan predicted the opposite ("brittle and hard rings, tough thuds"), and the ear
    // rejected it twice. Q is a count of oscillations, so the same Q at a higher frequency is a
    // shorter sound in seconds: stone's stiffness puts it near 880 Hz, and a hand-sized stone
    // cracks rather than rings. Wood is lower and outlasts it despite being the lossier of the
    // two. Material is told apart by brightness here, not by length.
    [Fact]
    public void WoodOnWoodOutlastsStoneOnStone()
    {
        var stoneOnStone = ImpactModel.Render(Stone, Stone, 1.0f, SampleRate, 7);
        var woodOnWood = ImpactModel.Render(Wood, Wood, 1.0f, SampleRate, 7);

        Assert.True(
            Analysis.DurationAbove(woodOnWood, SampleRate, -40.0f) > Analysis.DurationAbove(stoneOnStone, SampleRate, -40.0f));
    }

    [Fact]
    public void DoublingSizeRoughlyHalvesDominantFrequency()
    {
        var small = ImpactModel.Render(Stone, Stone, 1.0f, SampleRate, 99);
        var large = ImpactModel.Render(Stone, Stone, 2.0f, SampleRate, 99);

        var smallFrequency = Analysis.DominantFrequency(small, SampleRate);
        var largeFrequency = Analysis.DominantFrequency(large, SampleRate);

        Assert.InRange(largeFrequency, smallFrequency * 0.4f, smallFrequency * 0.6f);
    }

    // The buffer follows the decay of the longer-ringing body rather than a fixed length: two
    // pairings with very different decays must not come out the same number of samples.
    [Fact]
    public void RenderedLengthFollowsTheRingRatherThanBeingFixed()
    {
        var briefer = ImpactModel.Render(Stone, Stone, 1.0f, SampleRate, 3);
        var longer = ImpactModel.Render(Wood, Wood, 1.0f, SampleRate, 3);

        Assert.True(longer.Length > briefer.Length);
    }

    // A stiffer body is higher and therefore shorter at the same Q. This is the relationship the
    // decay now rests on, so it is pinned directly rather than only through the stone/wood pair.
    [Fact]
    public void AStifferBodyOfTheSameToughnessRingsForLessTime()
    {
        var soft = new ImpactMaterial(Hardness: 0.3f, Toughness: 0.4f, Density: 1.1f);
        var stiff = soft with { Hardness = 0.9f };

        var softSamples = ImpactModel.Render(soft, soft, 1.0f, SampleRate, 21);
        var stiffSamples = ImpactModel.Render(stiff, stiff, 1.0f, SampleRate, 21);

        Assert.True(
            Analysis.DurationAbove(softSamples, SampleRate, -40.0f) > Analysis.DurationAbove(stiffSamples, SampleRate, -40.0f));
        Assert.True(
            Analysis.DominantFrequency(stiffSamples, SampleRate) > Analysis.DominantFrequency(softSamples, SampleRate));
    }

    [Fact]
    public void RenderNeverProducesAnEmptyBuffer()
    {
        var samples = ImpactModel.Render(Stone, Wood, 1.0f, SampleRate, 5);

        Assert.NotEmpty(samples);
    }
}
