using ManyWinters.Core.Maps;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.Maps;

public class TerrainFeaturesTests
{
    private const string FeaturesSource = "res://Content/terrain/praha-liben/features.json";

    // A 3-4-5 segment, so its direction (0.6, 0.8) and normal (-0.8, 0.6) are exact and a
    // point can be put a chosen distance beside its middle.
    private const string SlantedLine = "[[0, 0], [30, 40]]";

    [Fact]
    public void NoneHasNoWaterNoRockAndNoWaterways()
    {
        var spot = new Position(3.5, -8.25);

        Assert.False(TerrainFeatures.None.IsWater(spot));
        Assert.False(TerrainFeatures.None.IsRock(spot));
        Assert.Empty(TerrainFeatures.None.Waterways);
    }

    [Fact]
    public void AWaterAreaCoversThePointsInsideItsRingAndNothingOutside()
    {
        var features = Load("""{ "waterAreas": [ { "rings": [ [[0, 0], [10, 0], [0, 7], [0, 0]] ] } ] }""");

        Assert.True(features.IsWater(new Position(2.5, 1.5)));
        Assert.False(features.IsWater(new Position(8, 6)));
        Assert.False(features.IsWater(new Position(-0.5, 2)));
        Assert.False(features.IsWater(new Position(2.5, -1)));
    }

    [Fact]
    public void AnInnerRingIsAHoleByTheEvenOddRule()
    {
        var features = Load("""{ "waterAreas": [ { "rings": [ [[-10, -10], [20, -10], [20, 15], [-10, 15], [-10, -10]], [[0, 0], [5, 0], [5, 3], [0, 3], [0, 0]] ] } ] }""");

        Assert.True(features.IsWater(new Position(10.5, 8.25)));
        Assert.True(features.IsWater(new Position(-6, 11)));
        Assert.False(features.IsWater(new Position(2.25, 1.5)));
        Assert.False(features.IsWater(new Position(30, 0)));
    }

    [Fact]
    public void AnIslandInsideAHoleIsWaterAgain()
    {
        var features = Load("""{ "waterAreas": [ { "rings": [ [[-20, -20], [20, -20], [20, 20], [-20, 20], [-20, -20]], [[-10, -10], [10, -10], [10, 10], [-10, 10], [-10, -10]], [[-2, -3], [4, -3], [4, 2], [-2, 2], [-2, -3]] ] } ] }""");

        Assert.True(features.IsWater(new Position(15, 12)));
        Assert.False(features.IsWater(new Position(7, -6)));
        Assert.True(features.IsWater(new Position(1, -1)));
    }

    [Fact]
    public void AWaterwayIsWaterWithinHalfItsWidthOfItsCenterline()
    {
        var features = Load($$"""{ "waterways": [ { "name": "Brook", "widthMeters": 4, "points": {{SlantedLine}} } ] }""");

        Assert.True(features.IsWater(new Position(15, 20)));
        Assert.True(features.IsWater(new Position(15 - (0.8 * 1.9), 20 + (0.6 * 1.9))));
        Assert.True(features.IsWater(new Position(15 + (0.8 * 1.9), 20 - (0.6 * 1.9))));
        Assert.False(features.IsWater(new Position(15 - (0.8 * 2.1), 20 + (0.6 * 2.1))));
        Assert.False(features.IsWater(new Position(15 + (0.8 * 2.1), 20 - (0.6 * 2.1))));
    }

    [Fact]
    public void AWaterAreaIsAWaterAreaButAWaterwayIsNot()
    {
        var features = Load($$"""{ "waterways": [ { "widthMeters": 4, "points": {{SlantedLine}} } ], "waterAreas": [ { "rings": [ [[40, 40], [52, 40], [40, 49], [40, 40]] ] } ] }""");
        var onTheWaterway = new Position(15, 20);
        var inTheArea = new Position(43.5, 42.25);

        Assert.True(features.IsWater(onTheWaterway));
        Assert.False(features.IsWaterArea(onTheWaterway));
        Assert.True(features.IsWaterArea(inTheArea));
        Assert.False(features.IsWaterArea(new Position(51, 48)));
    }

    [Fact]
    public void AWaterwayEndsRoundedHalfAWidthPastItsLastPoint()
    {
        var features = Load($$"""{ "waterways": [ { "name": "", "widthMeters": 4, "points": {{SlantedLine}} } ] }""");

        Assert.True(features.IsWater(new Position(30 + (0.6 * 1.5), 40 + (0.8 * 1.5))));
        Assert.False(features.IsWater(new Position(30 + (0.6 * 2.5), 40 + (0.8 * 2.5))));
        Assert.True(features.IsWater(new Position(-(0.6 * 1.5), -(0.8 * 1.5))));
        Assert.False(features.IsWater(new Position(-(0.6 * 2.5), -(0.8 * 2.5))));
    }

    [Fact]
    public void AWaterwayFollowsEverySegmentOfItsPolyline()
    {
        var features = Load("""{ "waterways": [ { "name": "", "widthMeters": 2, "points": [[0, 0], [30, 40], [30, 90]] } ] }""");

        Assert.True(features.IsWater(new Position(30.5, 70)));
        Assert.False(features.IsWater(new Position(31.5, 70)));
    }

    [Fact]
    public void ADegenerateWaterwaySegmentIsAPointOfWaterNotAnError()
    {
        var features = Load("""{ "waterways": [ { "name": "", "widthMeters": 2, "points": [[5, 5], [5, 5]] } ] }""");

        Assert.True(features.IsWater(new Position(5.5, 5.5)));
        Assert.False(features.IsWater(new Position(6.5, 5)));
    }

    [Fact]
    public void WaterwaysAreExposedWithTheirNameWidthAndPoints()
    {
        var features = Load("""{ "waterways": [ { "name": "Rokytka", "widthMeters": 3.5, "points": [[1.5, -2.25], [7, 9]] } ] }""");

        var waterway = Assert.Single(features.Waterways);
        Assert.Equal("Rokytka", waterway.Name);
        Assert.Equal(3.5, waterway.WidthMeters);
        Assert.Equal([new Position(1.5, -2.25), new Position(7, 9)], waterway.Points);
    }

    [Fact]
    public void ARockAreaIsRockInsideItsRingAndNotWater()
    {
        var features = Load("""{ "rockAreas": [ { "rings": [ [[0, 0], [10, 0], [0, 7], [0, 0]] ] } ] }""");

        Assert.True(features.IsRock(new Position(2.5, 1.5)));
        Assert.False(features.IsRock(new Position(8, 6)));
        Assert.False(features.IsWater(new Position(2.5, 1.5)));
    }

    [Fact]
    public void ACliffIsRockWithinThreeMetresOfItsLine()
    {
        var features = Load($$"""{ "cliffs": [ { "points": {{SlantedLine}} } ] }""");

        Assert.True(features.IsRock(new Position(15 - (0.8 * 2.9), 20 + (0.6 * 2.9))));
        Assert.True(features.IsRock(new Position(15 + (0.8 * 2.9), 20 - (0.6 * 2.9))));
        Assert.False(features.IsRock(new Position(15 - (0.8 * 3.1), 20 + (0.6 * 3.1))));
        Assert.False(features.IsRock(new Position(15 + (0.8 * 3.1), 20 - (0.6 * 3.1))));
        Assert.False(features.IsWater(new Position(15, 20)));
    }

    [Fact]
    public void AWaterAreasSignedDistanceIsTheGapToItsNearestEdgePositiveInside()
    {
        // A 40 x 30 rectangle with a 10 x 6 hole; distances to the nearest edge are exact.
        var features = Load("""{ "waterAreas": [ { "rings": [ [[0, 0], [40, 0], [40, 30], [0, 30], [0, 0]], [[20, 10], [30, 10], [30, 16], [20, 16], [20, 10]] ] } ] }""");

        Assert.Equal(3.5, features.WaterAreaSignedDistance(new Position(3.5, 21)), 6);
        Assert.Equal(-2.25, features.WaterAreaSignedDistance(new Position(42.25, 17)), 6);
        Assert.Equal(-1.5, features.WaterAreaSignedDistance(new Position(23, 11.5)), 6);
        Assert.Equal(-5, features.WaterAreaSignedDistance(new Position(-3, -4)), 6);
    }

    [Fact]
    public void TheNearerOfTwoWaterAreasDecidesItsSignedDistance()
    {
        var features = Load("""{ "waterAreas": [ { "rings": [ [[0, 0], [10, 0], [10, 10], [0, 10], [0, 0]] ] }, { "rings": [ [[17, 0], [30, 0], [30, 10], [17, 10], [17, 0]] ] } ] }""");

        Assert.Equal(-2.5, features.WaterAreaSignedDistance(new Position(14.5, 5)), 6);
        Assert.Equal(1.5, features.WaterAreaSignedDistance(new Position(18.5, 5)), 6);
    }

    [Fact]
    public void WithoutAreasEveryPointIsInfinitelyFarFromWaterAndRock()
    {
        var features = Load($$"""{ "waterways": [ { "widthMeters": 4, "points": {{SlantedLine}} } ] }""");

        Assert.Equal(double.NegativeInfinity, features.WaterAreaSignedDistance(new Position(15, 20)));
        Assert.Equal(double.NegativeInfinity, features.RockSignedDistance(new Position(15, 20)));
    }

    [Fact]
    public void RockSignedDistanceTakesTheNearerOfAnAreaAndACliffStrip()
    {
        var features = Load($$"""{ "rockAreas": [ { "rings": [ [[-30, 0], [-20, 0], [-20, 8], [-30, 8], [-30, 0]] ] } ], "cliffs": [ { "points": {{SlantedLine}} } ] }""");

        // 1.25 m off the cliff's line: 3 - 1.25 inside the strip.
        Assert.Equal(1.75, features.RockSignedDistance(new Position(15 - (0.8 * 1.25), 20 + (0.6 * 1.25))), 6);
        Assert.Equal(-1.5, features.RockSignedDistance(new Position(15 + (0.8 * 4.5), 20 - (0.6 * 4.5))), 6);
        Assert.Equal(2.5, features.RockSignedDistance(new Position(-22.5, 5)), 6);
    }

    [Fact]
    public void ADocumentWithOnlySomeKindsOfFeatureLoadsTheRestAsEmpty()
    {
        var features = Load("""{ "source": "somewhere" }""");

        Assert.Empty(features.Waterways);
        Assert.False(features.IsWater(new Position(1, 1)));
        Assert.False(features.IsRock(new Position(1, 1)));
    }

    [Fact]
    public void LoadFromJsonTakesFeaturesJsonAndLeavesTheHeightmapAlone()
    {
        var features = TerrainFeatures.LoadFromJson(
        [
            ("res://Content/terrain/praha-liben/heightmap.json", """{ "gridSize": 41, "heights": [[1, 2]] }"""),
            (FeaturesSource, """{ "rockAreas": [ { "rings": [ [[0, 0], [10, 0], [0, 7], [0, 0]] ] } ] }"""),
        ]);

        Assert.True(features.IsRock(new Position(2.5, 1.5)));
    }

    [Fact]
    public void LoadFromJsonWithNoFeaturesDocumentGivesNone()
    {
        var features = TerrainFeatures.LoadFromJson([("res://Content/terrain/praha-liben/heightmap.json", "{}"), ("res://Content/terrain/my-features.json", "{}")]);

        Assert.Same(TerrainFeatures.None, features);
        Assert.Same(TerrainFeatures.None, TerrainFeatures.LoadFromJson([]));
    }

    [Fact]
    public void LoadFromJsonRefusesToPickBetweenTwoFeaturesDocuments()
    {
        var exception = Assert.Throws<InvalidDataException>(() => TerrainFeatures.LoadFromJson(
        [
            ("res://Content/terrain/a/features.json", "{}"),
            ("res://Content/terrain/b/features.json", "{}"),
        ]));

        Assert.Contains("res://Content/terrain/a/features.json", exception.Message);
        Assert.Contains("res://Content/terrain/b/features.json", exception.Message);
    }

    [Fact]
    public void LoadFromJsonNamesTheSourceOfADocumentThatDoesNotParse()
    {
        var broken = Assert.Throws<InvalidDataException>(() => TerrainFeatures.LoadFromJson([(FeaturesSource, "{ not json")]));
        var empty = Assert.Throws<InvalidDataException>(() => TerrainFeatures.LoadFromJson([(FeaturesSource, "null")]));

        Assert.Contains(FeaturesSource, broken.Message);
        Assert.Contains(FeaturesSource, empty.Message);
    }

    private static TerrainFeatures Load(string json) => TerrainFeatures.LoadFromJson([(FeaturesSource, json)]);
}
