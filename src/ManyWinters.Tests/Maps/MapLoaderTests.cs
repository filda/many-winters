using ManyWinters.Core.Maps;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Maps;

public class MapLoaderTests
{
    private static LoadedMap LoadDefault() => MapLoader.LoadDefault(TestCatalogs.CreateConfiguration());

    [Fact]
    public void LoadDefaultReturnsTheCampCenterUsedToPlaceEverything()
    {
        var map = LoadDefault();

        Assert.Equal(new Position(5, 250), map.CampCenter);
    }

    [Fact]
    public void LoadDefaultWiresTheGivenConfigurationIntoTheReturnedWorld()
    {
        var configuration = TestCatalogs.CreateConfiguration();

        var map = MapLoader.LoadDefault(configuration);

        Assert.Same(configuration.ResourceCatalog, map.World.ResourceCatalog);
        Assert.Same(configuration.SkillCatalog, map.World.SkillCatalog);
        Assert.Same(configuration.RecipeCatalog, map.World.RecipeCatalog);
        Assert.Same(configuration.BuildingCatalog, map.World.BuildingCatalog);
        Assert.Same(configuration.ItemCatalog, map.World.ItemCatalog);
        Assert.Same(configuration.SeasonParameters, map.World.SeasonParameters);
    }

    [Fact]
    public void LoadDefaultPopulatesTheWorldWithFifteenPeopleArrangedInAFiveColumnGrid()
    {
        var map = LoadDefault();

        var expectedNames = new[]
        {
            "Ava", "Bran", "Tora", "Kael", "Mira", "Doran", "Liska", "Faro",
            "Ivy", "Rask", "Sela", "Bodin", "Yara", "Corin", "Vessa",
        };
        var expectedPositions = new[]
        {
            new Position(1f, 248f), new Position(3f, 248f), new Position(5f, 248f), new Position(7f, 248f), new Position(9f, 248f),
            new Position(1f, 250f), new Position(3f, 250f), new Position(5f, 250f), new Position(7f, 250f), new Position(9f, 250f),
            new Position(1f, 252f), new Position(3f, 252f), new Position(5f, 252f), new Position(7f, 252f), new Position(9f, 252f),
        };

        Assert.Equal(15, map.World.People.Count);
        Assert.Equal(expectedNames, map.World.People.Select(p => p.Name));
        Assert.Equal(expectedPositions, map.World.People.Select(p => p.Position));
    }

    [Fact]
    public void LoadDefaultGivesStartingPeopleAVariedNonZeroAgeSpread()
    {
        var map = LoadDefault();

        var expectedAges = new long[] { 2, 4, 8, 1, 5, 3, 9, 2, 6, 1, 4, 7, 2, 3, 5 };

        Assert.Equal(expectedAges, map.World.People.Select(p => map.World.AgeInYears(p)));
    }

    [Fact]
    public void LoadDefaultAssignsFamilyTiesForEachOfTheThreeStartingCouplesChildren()
    {
        var map = LoadDefault();
        var byName = map.World.People.ToDictionary(p => p.Name);

        Assert.Equal(byName["Sela"].Id, byName["Ava"].MotherId);
        Assert.Equal(byName["Bran"].Id, byName["Ava"].FatherId);
        Assert.Equal(byName["Sela"].Id, byName["Faro"].MotherId);
        Assert.Equal(byName["Bran"].Id, byName["Faro"].FatherId);

        Assert.Equal(byName["Tora"].Id, byName["Mira"].MotherId);
        Assert.Equal(byName["Liska"].Id, byName["Mira"].FatherId);
        Assert.Equal(byName["Tora"].Id, byName["Vessa"].MotherId);
        Assert.Equal(byName["Liska"].Id, byName["Vessa"].FatherId);

        Assert.Equal(byName["Ivy"].Id, byName["Doran"].MotherId);
        Assert.Equal(byName["Bodin"].Id, byName["Doran"].FatherId);
        Assert.Equal(byName["Ivy"].Id, byName["Corin"].MotherId);
        Assert.Equal(byName["Bodin"].Id, byName["Corin"].FatherId);
    }

    [Fact]
    public void LoadDefaultLeavesSomeStartingPeopleWithNoRecordedParents()
    {
        var map = LoadDefault();
        var byName = map.World.People.ToDictionary(p => p.Name);

        foreach (var name in new[] { "Kael", "Rask", "Yara" })
        {
            Assert.Null(byName[name].MotherId);
            Assert.Null(byName[name].FatherId);
        }
    }

    [Fact]
    public void LoadDefaultPopulatesTheWorldWithTheExpectedResourceNodes()
    {
        var map = LoadDefault();

        var expected = new[]
        {
            (TestCatalogs.Apple, new Position(-1f, 255f), 200f),
            (TestCatalogs.Pear, new Position(5f, 245f), 200f),
            (TestCatalogs.Mushroom, new Position(11f, 255f), 200f),
            (TestCatalogs.Potato, new Position(-1f, 245f), 200f),
            (TestCatalogs.Apple, new Position(11f, 245f), 200f),
            (TestCatalogs.Wood, new Position(5f, 255f), 300f),
        };

        Assert.Equal(6, map.World.ResourceNodes.Count);
        Assert.Equal(expected, map.World.ResourceNodes.Select(n => (n.Kind, n.Position, n.RemainingAmount)));
    }

    [Fact]
    public void LoadDefaultStartsWithNoBuildings()
    {
        var map = LoadDefault();

        Assert.Empty(map.World.Buildings);
    }
}
