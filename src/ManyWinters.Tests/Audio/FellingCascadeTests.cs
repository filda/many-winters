using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class FellingCascadeTests
{
    private const int SampleRate = 22050;

    // Shipped materials from ImpactModelTests: stone is hard and dense but brittle, wood is soft,
    // light and tough. The tree itself is wood; the tool is stone, standing in for an axe head.
    private static readonly ImpactMaterial Stone = new(Hardness: 1.0f, Toughness: 0.15f, Density: 2.0f);
    private static readonly ImpactMaterial Wood = new(Hardness: 0.4f, Toughness: 0.7f, Density: 0.5f);

    // Oblique on purpose, and chopCount kept at 5 rather than 1 or 2: with only a couple of chops
    // every one of them counts as one of the "last two, landed harder", which folds a whole extra
    // knob into the same handful of events and makes the early chopping busier than the scene
    // intends. Five is past that edge without being a round number either.
    private static readonly FellingTree Standard = new(Size: 0.55f, Wood: Wood, Tool: Stone);
    private const int StandardChopCount = 5;

    [Fact]
    public void SameSeedRendersAnIdenticalBuffer()
    {
        var first = FellingCascade.Render(Standard, StandardChopCount, SampleRate, 613);
        var second = FellingCascade.Render(Standard, StandardChopCount, SampleRate, 613);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentSeedsRenderDifferentBuffers()
    {
        var first = FellingCascade.Render(Standard, StandardChopCount, SampleRate, 613);
        var second = FellingCascade.Render(Standard, StandardChopCount, SampleRate, 614);

        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData(0.2f, 3)]
    [InlineData(1.4f, 7)]
    public void EveryRenderNormalisesToPeak09Exactly(float size, int chopCount)
    {
        var samples = FellingCascade.Render(Standard with { Size = size }, chopCount, SampleRate, 42);

        Assert.Equal(0.9f, Analysis.Peak(samples), 4);
    }

    // Two thresholds, not one: a single threshold lets a decaying tail's own noise floor blip back
    // over it and register as another chop, which is exactly what cost FrictionModelTests a round.
    // The gap between rise and fall is what a real chop-to-chop silence has to cross and a blip
    // does not.
    [Fact]
    public void ChopTransientsAreCountableBeforeTheCreakBegins()
    {
        var samples = FellingCascade.Render(Standard, StandardChopCount, SampleRate, 7);

        // The creak only starts a further 0.35 s after the last chop's own decay, so a cutoff at
        // the nominal onset of the (chopCount)th chop plus a fixed margin comfortably includes
        // every chop's tail without ever reaching into the creak.
        var cutoffSeconds = ((StandardChopCount - 1) * 0.85f) + 0.5f;
        var cutoffSample = Math.Min(samples.Length, (int)(cutoffSeconds * SampleRate));
        var beforeCreak = samples.AsSpan(0, cutoffSample).ToArray();

        Assert.Equal(StandardChopCount, CountTransients(beforeCreak));
    }

    // The ground taking the whole trunk's weight has to be louder than every earlier layer and has
    // to sit in the back third of the scene, not merely somewhere after the chops.
    [Fact]
    public void TheCrashIsTheLoudestMomentAndLandsInTheLastThird()
    {
        var samples = FellingCascade.Render(Standard, StandardChopCount, SampleRate, 613);
        var windowRms = WindowRms(samples);

        var lastThirdStart = windowRms.Length * 2 / 3;
        var maxBeforeLastThird = windowRms.Take(lastThirdStart).Max();
        var maxInLastThird = windowRms.Skip(lastThirdStart).Max();

        Assert.True(
            maxInLastThird > maxBeforeLastThird,
            $"before={maxBeforeLastThird}, last third={maxInLastThird}");
    }

    // A bigger tree's every layer scales with Size: a longer creak, a bigger crack and a bigger
    // crash whose own ImpactModel modes sit lower for the same reason a bigger struck body does
    // anywhere else in this library. The crash region is picked out by its own windowed-RMS peak
    // rather than assumed to be the last few hundred milliseconds, since the tail silence after it
    // would otherwise dominate a naive end-of-buffer slice.
    [Fact]
    public void ABiggerTreeRunsLongerAndIsLower()
    {
        var small = FellingCascade.Render(Standard with { Size = 0.3f }, StandardChopCount, SampleRate, 123);
        var large = FellingCascade.Render(Standard with { Size = 1.2f }, StandardChopCount, SampleRate, 123);

        Assert.True(large.Length > small.Length);
        Assert.True(CrashRegionDominantFrequency(large) < CrashRegionDominantFrequency(small));
    }

    [Fact]
    public void TheTotalLengthFollowsChopCount()
    {
        var fewer = FellingCascade.Render(Standard, 3, SampleRate, 9);
        var more = FellingCascade.Render(Standard, 8, SampleRate, 9);

        Assert.True(more.Length > fewer.Length);
    }

    private const float WindowSeconds = 0.05f;

    private static float[] WindowRms(float[] samples)
    {
        var windowSamples = (int)(WindowSeconds * SampleRate);
        var windowCount = samples.Length / windowSamples;

        var result = new float[windowCount];
        for (var w = 0; w < windowCount; w++)
        {
            result[w] = Analysis.Rms(samples.AsSpan(w * windowSamples, windowSamples));
        }

        return result;
    }

    private static int CountTransients(float[] samples)
    {
        var windowRms = WindowRms(samples);
        var peak = windowRms.Max();

        var rise = peak * 0.35f;
        var fall = peak * 0.15f;

        var count = 0;
        var above = false;
        foreach (var rms in windowRms)
        {
            if (!above && rms > rise)
            {
                count++;
                above = true;
            }
            else if (above && rms < fall)
            {
                above = false;
            }
        }

        return count;
    }

    private const float CrashRegionSeconds = 0.3f;

    private static float CrashRegionDominantFrequency(float[] samples)
    {
        var windowRms = WindowRms(samples);
        var peakWindow = Array.IndexOf(windowRms, windowRms.Max());
        var peakSample = peakWindow * (int)(WindowSeconds * SampleRate);

        var start = Math.Max(0, peakSample - (int)(CrashRegionSeconds / 2.0f * SampleRate));
        var length = Math.Min((int)(CrashRegionSeconds * SampleRate), samples.Length - start);

        return Analysis.DominantFrequency(samples.AsSpan(start, length), SampleRate);
    }
}
