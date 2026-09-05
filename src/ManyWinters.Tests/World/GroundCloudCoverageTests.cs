using ManyWinters.Core.World;

namespace ManyWinters.Tests.World;

public class GroundCloudCoverageTests
{
    [Fact]
    public void ExploredGroundAndItsImmediateSurroundingsGetNoCloud()
    {
        Assert.Equal(0f, GroundCloudCoverage.Coverage(0f));
        Assert.Equal(0f, GroundCloudCoverage.Coverage(GroundCloudCoverage.HugDistanceMeters - 0.1f));
    }

    [Fact]
    public void CoverageIsCompleteRightAtTheBoundary()
    {
        Assert.Equal(1f, GroundCloudCoverage.Coverage(GroundCloudCoverage.HugDistanceMeters), 5);
    }

    [Fact]
    public void CoverageThinsWithDistance()
    {
        var band = GroundCloudCoverage.MaxDistanceMeters - GroundCloudCoverage.HugDistanceMeters;
        var near = GroundCloudCoverage.Coverage(GroundCloudCoverage.HugDistanceMeters + (band * 0.25f));
        var mid = GroundCloudCoverage.Coverage(GroundCloudCoverage.HugDistanceMeters + (band * 0.5f));
        var far = GroundCloudCoverage.Coverage(GroundCloudCoverage.HugDistanceMeters + (band * 0.9f));

        Assert.True(near > mid);
        Assert.True(mid > far);
        Assert.True(far > 0f);
    }

    [Fact]
    public void CoverageStaysNearlyFullJustPastTheBoundary()
    {
        var band = GroundCloudCoverage.MaxDistanceMeters - GroundCloudCoverage.HugDistanceMeters;

        Assert.True(GroundCloudCoverage.Coverage(GroundCloudCoverage.HugDistanceMeters + (band * 0.1f)) > 0.8f);
    }

    [Fact]
    public void NothingIsPlacedBeyondTheMaximumDistance()
    {
        Assert.Equal(0f, GroundCloudCoverage.Coverage(GroundCloudCoverage.MaxDistanceMeters), 5);
        Assert.Equal(0f, GroundCloudCoverage.Coverage(GroundCloudCoverage.MaxDistanceMeters + 0.1f));
        Assert.Equal(0f, GroundCloudCoverage.Coverage(GridDistanceField.Unreachable));
    }

    [Fact]
    public void ShouldShowComparesTheRollAgainstCoverage()
    {
        var atBoundary = GroundCloudCoverage.HugDistanceMeters;
        var midway = (GroundCloudCoverage.HugDistanceMeters + GroundCloudCoverage.MaxDistanceMeters) / 2f;
        var midwayCoverage = GroundCloudCoverage.Coverage(midway);

        Assert.True(GroundCloudCoverage.ShouldShow(atBoundary, 0.999f));
        Assert.True(GroundCloudCoverage.ShouldShow(midway, midwayCoverage - 0.01f));
        Assert.False(GroundCloudCoverage.ShouldShow(midway, midwayCoverage + 0.01f));
        Assert.False(GroundCloudCoverage.ShouldShow(0f, 0f));
    }
}
