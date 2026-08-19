using ManyWinters.Core.Maps;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Maps;

public class MapLoaderTests
{
    [Fact]
    public void LoadDefaultReturnsTheExpectedTerrainSize()
    {
        var map = MapLoader.LoadDefault(TestCatalogs.CreateResourceCatalog(), TestCatalogs.CreateSkillCatalog());

        Assert.Equal(20f, map.TerrainWidth);
        Assert.Equal(20f, map.TerrainDepth);
    }

    [Fact]
    public void LoadDefaultWiresTheGivenCatalogsIntoTheReturnedWorld()
    {
        var resourceCatalog = TestCatalogs.CreateResourceCatalog();
        var skillCatalog = TestCatalogs.CreateSkillCatalog();

        var map = MapLoader.LoadDefault(resourceCatalog, skillCatalog);

        Assert.Same(resourceCatalog, map.World.ResourceCatalog);
        Assert.Same(skillCatalog, map.World.SkillCatalog);
    }

    [Fact]
    public void LoadDefaultPopulatesTheWorldWithFifteenPeopleArrangedInAFiveColumnGrid()
    {
        var map = MapLoader.LoadDefault(TestCatalogs.CreateResourceCatalog(), TestCatalogs.CreateSkillCatalog());

        var expectedNames = Enumerable.Range(1, 15).Select(i => $"Person {i}");
        var expectedPositions = new[]
        {
            new Position(-4f, -2f), new Position(-2f, -2f), new Position(0f, -2f), new Position(2f, -2f), new Position(4f, -2f),
            new Position(-4f, 0f), new Position(-2f, 0f), new Position(0f, 0f), new Position(2f, 0f), new Position(4f, 0f),
            new Position(-4f, 2f), new Position(-2f, 2f), new Position(0f, 2f), new Position(2f, 2f), new Position(4f, 2f),
        };

        Assert.Equal(15, map.World.People.Count);
        Assert.Equal(expectedNames, map.World.People.Select(p => p.Name));
        Assert.Equal(expectedPositions, map.World.People.Select(p => p.Position));
    }

    [Fact]
    public void LoadDefaultPopulatesTheWorldWithTheExpectedResourceNodes()
    {
        var map = MapLoader.LoadDefault(TestCatalogs.CreateResourceCatalog(), TestCatalogs.CreateSkillCatalog());

        var expected = new[]
        {
            (TestCatalogs.Apple, new Position(-6f, 5f), 200f),
            (TestCatalogs.Pear, new Position(0f, -5f), 200f),
            (TestCatalogs.Mushroom, new Position(6f, 5f), 200f),
            (TestCatalogs.Potato, new Position(-6f, -5f), 200f),
            (TestCatalogs.Apple, new Position(6f, -5f), 200f),
        };

        Assert.Equal(5, map.World.ResourceNodes.Count);
        Assert.Equal(expected, map.World.ResourceNodes.Select(n => (n.Kind, n.Position, n.RemainingAmount)));
    }
}
