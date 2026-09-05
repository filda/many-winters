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
    public void CoverageAtTheBoundaryIsTheBoundaryShare()
    {
        Assert.Equal(GroundCloudCoverage.BoundaryCoverage, GroundCloudCoverage.Coverage(GroundCloudCoverage.HugDistanceMeters), 5);
    }

    [Fact]
    public void CoverageGrowsWithDistance()
    {
        var band = GroundCloudCoverage.FullCoverageDistanceMeters - GroundCloudCoverage.HugDistanceMeters;
        var near = GroundCloudCoverage.Coverage(GroundCloudCoverage.HugDistanceMeters + (band * 0.25f));
        var mid = GroundCloudCoverage.Coverage(GroundCloudCoverage.HugDistanceMeters + (band * 0.5f));
        var far = GroundCloudCoverage.Coverage(GroundCloudCoverage.HugDistanceMeters + (band * 0.9f));

        Assert.True(near < mid);
        Assert.True(mid < far);
    }

    [Fact]
    public void CoverageIsCompleteFromTheFullCoverageDistanceOn()
    {
        var pastFull = (GroundCloudCoverage.FullCoverageDistanceMeters + GroundCloudCoverage.MaxDistanceMeters) / 2f;

        Assert.Equal(1f, GroundCloudCoverage.Coverage(GroundCloudCoverage.FullCoverageDistanceMeters), 5);
        Assert.Equal(1f, GroundCloudCoverage.Coverage(pastFull), 5);
    }

    [Fact]
    public void NothingIsPlacedBeyondTheMaximumDistance()
    {
        Assert.Equal(0f, GroundCloudCoverage.Coverage(GroundCloudCoverage.MaxDistanceMeters + 0.1f));
        Assert.Equal(0f, GroundCloudCoverage.Coverage(GridDistanceField.Unreachable));
    }

    [Fact]
    public void ShouldShowComparesTheRollAgainstCoverage()
    {
        var atBoundary = GroundCloudCoverage.HugDistanceMeters;

        Assert.True(GroundCloudCoverage.ShouldShow(atBoundary, GroundCloudCoverage.BoundaryCoverage - 0.01f));
        Assert.False(GroundCloudCoverage.ShouldShow(atBoundary, GroundCloudCoverage.BoundaryCoverage + 0.01f));
        Assert.True(GroundCloudCoverage.ShouldShow(GroundCloudCoverage.FullCoverageDistanceMeters, 0.999f));
        Assert.False(GroundCloudCoverage.ShouldShow(0f, 0f));
    }
}
