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
    public void LoadDefaultPopulatesTheWorldWithFifteenPeopleAsANamedList()
    {
        var map = LoadDefault();

        var expectedNames = new[]
        {
            "Ava", "Bran", "Tora", "Kael", "Mira", "Doran", "Liska", "Faro",
            "Ivy", "Rask", "Sela", "Bodin", "Yara", "Corin", "Vessa",
        };

        Assert.Equal(15, map.World.People.Count);
        Assert.Equal(expectedNames, map.World.People.Select(p => p.Name));
    }

    [Fact]
    public void LoadDefaultScattersStartingPeopleOrganicallyAroundTheCampCenterInsteadOfAGrid()
    {
        var map = LoadDefault();

        const float crowdRadius = 4f;
        const float minSpacing = 1f;
        var positions = map.World.People.Select(p => p.Position).ToList();

        Assert.All(positions, p => Assert.True(WorldState.Distance(p, map.CampCenter) <= crowdRadius));
        for (var i = 0; i < positions.Count; i++)
        {
            for (var j = i + 1; j < positions.Count; j++)
            {
                Assert.True(WorldState.Distance(positions[i], positions[j]) >= minSpacing);
            }
        }

        // Not a grid: no two starting people share an X or a Z (a 5-column grid stacked
        // several people on each of five X values and each of three Z values).
        Assert.Equal(positions.Count, positions.Select(p => p.X).Distinct().Count());
        Assert.Equal(positions.Count, positions.Select(p => p.Y).Distinct().Count());
    }

    [Fact]
    public void LoadDefaultPlacesStartingPeopleDeterministically()
    {
        var firstRun = LoadDefault().World.People.Select(p => p.Position).ToList();
        var secondRun = LoadDefault().World.People.Select(p => p.Position).ToList();

        Assert.Equal(firstRun, secondRun);
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
    public void LoadDefaultPopulatesTheWorldWithTheHandPlacedFoodAndWoodNodesFirst()
    {
        var map = LoadDefault();

        // The hand-placed starting supply (fruit/wood/grass near camp) is spawned before
        // ScatterDecorations runs, so these are always the first 7 nodes regardless of how
        // many procedural decoration nodes follow them.
        var expectedFirstSeven = new[]
        {
            (TestCatalogs.Apple, new Position(-1f, 255f), 200f),
            (TestCatalogs.Pear, new Position(5f, 245f), 200f),
            (TestCatalogs.Mushroom, new Position(11f, 255f), 200f),
            (TestCatalogs.Potato, new Position(-1f, 245f), 200f),
            (TestCatalogs.Apple, new Position(11f, 245f), 200f),
            (TestCatalogs.Wood, new Position(5f, 255f), 300f),
            (TestCatalogs.Grass, new Position(15f, 250f), 200f),
        };

        Assert.Equal(expectedFirstSeven, map.World.ResourceNodes.Take(7).Select(n => (n.Kind, n.Position, n.RemainingAmount)));
    }

    [Fact]
    public void LoadDefaultScattersEveryDecorationKindAsRealResourceNodes()
    {
        var map = LoadDefault();

        // Former terrain decoration (todo #7) - every kind that used to be a purely-visual
        // sprite must now be a real, individually-gatherable ResourceNode, in the thousands
        // (dense zone + wide pass + several groves), not just a handful.
        var decorationKinds = new[]
        {
            TestCatalogs.ConiferTree, TestCatalogs.DeciduousTree, TestCatalogs.Bush,
            TestCatalogs.Grass, TestCatalogs.Flower, TestCatalogs.Fern,
            TestCatalogs.RockPile, TestCatalogs.RockBoulder, TestCatalogs.RockCluster,
            TestCatalogs.TreeStump, TestCatalogs.FallenLog,
        };

        var countsByKind = map.World.ResourceNodes
            .Skip(7)
            .GroupBy(n => n.Kind)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.All(decorationKinds, kind => Assert.True(countsByKind.GetValueOrDefault(kind) > 0, $"Expected at least one '{kind}' decoration node."));
        Assert.True(map.World.ResourceNodes.Count > 5000, "Expected thousands of scattered decoration nodes.");
    }

    [Fact]
    public void LoadDefaultScattersDecorationsDeterministically()
    {
        var firstRun = LoadDefault().World.ResourceNodes.Select(n => (n.Kind, n.Position, n.RemainingAmount)).ToList();
        var secondRun = LoadDefault().World.ResourceNodes.Select(n => (n.Kind, n.Position, n.RemainingAmount)).ToList();

        Assert.Equal(firstRun, secondRun);
    }

    [Fact]
    public void LoadDefaultNeverScattersTwoDecorationsWithinMinimumSpacing()
    {
        var map = LoadDefault();

        // Sampled rather than an exhaustive O(n^2) check (thousands of nodes) - a spot check
        // against MapLoader's own spatial-hash rejection sampling (MinDecorationSpacing) is
        // enough to catch a regression in that mechanism without a slow all-pairs test.
        var positions = map.World.ResourceNodes.Skip(7).Select(n => n.Position).Take(500).ToList();
        for (var i = 0; i < positions.Count; i++)
        {
            for (var j = i + 1; j < positions.Count; j++)
            {
                Assert.True(WorldState.Distance(positions[i], positions[j]) >= 0.1f);
            }
        }
    }

    [Fact]
    public void LoadDefaultPlacesTheStartingCrowdAtFixedPositions()
    {
        var map = LoadDefault();

        // The scatter is seeded, not time-based, so the band starts every new game standing in
        // the same spots - the same reason the ages and family ties are fixed arrays. Pinned to
        // six decimals rather than exactly: the positions come out of Math.Cos/Sin, whose last
        // bit isn't guaranteed identical across platforms.
        var expected = new[]
        {
            (5.011135470528585, 251.3310821297698), (1.5615704747456682, 250.72309636440173),
            (3.555535792082975, 247.80044441469136), (2.635896661616461, 253.0842846534414),
            (7.578738340989338, 251.9050977544539), (6.9599408054416685, 250.3565085799907),
            (4.3592773602841675, 253.0716472830186), (4.04999725361762, 246.78655300515015),
            (6.1620266058447, 249.62064547240826), (6.312726331411297, 252.09482381648456),
            (5.488608425447254, 248.42750635731426), (6.668075163220313, 246.62345770063044),
            (1.8232888860379601, 248.82686291688273), (4.853812141455856, 249.5567624203569),
            (8.587093795870151, 249.816741228071),
        };

        Assert.Equal(expected.Length, map.World.People.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Item1, map.World.People[i].Position.X, 6);
            Assert.Equal(expected[i].Item2, map.World.People[i].Position.Y, 6);
        }
    }

    [Fact]
    public void LoadDefaultBuildsAWorldOfTheSameComposition()
    {
        var map = LoadDefault();

        // Every count here is decided by MapLoader alone: the fixed dense-zone/grove counts,
        // and - for everything the open-world pass adds on top - the seeded noise fields and
        // the placement rejection. Which makes this the one place a change in world generation
        // shows up as a number rather than as "the world looks a bit different now". Update it
        // deliberately when the generation is retuned; a surprise change here is a bug.
        var expected = new Dictionary<ResourceKindId, int>
        {
            [TestCatalogs.Grass] = 7281,
            [TestCatalogs.Fern] = 3285,
            [TestCatalogs.Flower] = 2039,
            [TestCatalogs.Bush] = 1635,
            [TestCatalogs.ConiferTree] = 883,
            [TestCatalogs.DeciduousTree] = 585,
            [TestCatalogs.RockPile] = 560,
            [TestCatalogs.RockCluster] = 559,
            [TestCatalogs.RockBoulder] = 554,
            [TestCatalogs.TreeStump] = 61,
            [TestCatalogs.FallenLog] = 33,
            [TestCatalogs.Apple] = 2,
            [TestCatalogs.Pear] = 1,
            [TestCatalogs.Mushroom] = 1,
            [TestCatalogs.Potato] = 1,
            [TestCatalogs.Wood] = 1,
        };

        var actual = map.World.ResourceNodes.GroupBy(n => n.Kind).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(expected.OrderBy(kv => kv.Key.Value, StringComparer.Ordinal), actual.OrderBy(kv => kv.Key.Value, StringComparer.Ordinal));
        Assert.Equal(17481, map.World.ResourceNodes.Count);
    }

    [Fact]
    public void LoadDefaultGivesEachScatteredKindTheYieldThatMatchesIt()
    {
        var map = LoadDefault();

        // Renewable canopy/ground cover gets an amount in line with the hand-placed fruit
        // trees; the finite ones (rock, stump, log) get a smaller one-shot amount because they
        // never come back once spent. The hand-placed grass node near camp is the one
        // scattered kind that also exists at its own larger starting amount.
        var amountsByKind = map.World.ResourceNodes
            .GroupBy(n => n.Kind)
            .ToDictionary(g => g.Key, g => g.Select(n => n.RemainingAmount).Distinct().OrderBy(a => a).ToArray());

        var expected = new Dictionary<ResourceKindId, float[]>
        {
            [TestCatalogs.ConiferTree] = [200f],
            [TestCatalogs.DeciduousTree] = [200f],
            [TestCatalogs.Bush] = [200f],
            [TestCatalogs.Grass] = [100f, 200f],
            [TestCatalogs.Flower] = [100f],
            [TestCatalogs.Fern] = [100f],
            [TestCatalogs.RockPile] = [80f],
            [TestCatalogs.RockBoulder] = [80f],
            [TestCatalogs.RockCluster] = [80f],
            [TestCatalogs.TreeStump] = [60f],
            [TestCatalogs.FallenLog] = [60f],
        };

        Assert.All(expected, kv => Assert.Equal(kv.Value, amountsByKind[kv.Key]));
    }

    [Fact]
    public void LoadDefaultKeepsEveryScatteredNodeOnTheTerrainPatch()
    {
        var map = LoadDefault();

        // The open world scatters across the real terrain patch's half-extent (500 m); a grove
        // whose center lands right at that edge reaches one grove radius (65 m) further out,
        // and nothing at all belongs beyond that - a node off the heightmap has no ground under
        // it to stand on.
        const double limit = 500 + 65;

        Assert.All(map.World.ResourceNodes, n =>
        {
            Assert.InRange(n.Position.X, -limit, limit);
            Assert.InRange(n.Position.Y, -limit, limit);
        });

        // ...and it really does reach out that far, rather than huddling near camp.
        Assert.True(map.World.ResourceNodes.Any(n => n.Position.X < -400), "Nothing was scattered along the far western edge.");
        Assert.True(map.World.ResourceNodes.Any(n => n.Position.X > 400), "Nothing was scattered along the far eastern edge.");
        Assert.True(map.World.ResourceNodes.Any(n => n.Position.Y < -400), "Nothing was scattered along the far southern edge.");
        Assert.True(map.World.ResourceNodes.Any(n => n.Position.Y > 400), "Nothing was scattered along the far northern edge.");
    }

    [Fact]
    public void LoadDefaultGrowsEveryBiomeBandOutInTheOpenWorld()
    {
        var map = LoadDefault();

        // Well beyond the dense zone (110 m) and further out than any grove reaches from camp,
        // so what's left is the open-world noise pass alone. Each of its four bands has to have
        // actually produced something: forest, thicket, meadow, and the rocky ground everything
        // below the meadow threshold falls back to.
        var outThere = map.World.ResourceNodes
            .Where(n => WorldState.Distance(n.Position, map.CampCenter) > 200)
            .GroupBy(n => n.Kind)
            .ToDictionary(g => g.Key, g => g.Count());

        var perBand = new (string Band, ResourceKindId[] Kinds)[]
        {
            ("forest", [TestCatalogs.ConiferTree, TestCatalogs.DeciduousTree]),
            ("thicket", [TestCatalogs.Bush, TestCatalogs.Fern]),
            ("meadow", [TestCatalogs.Grass, TestCatalogs.Flower, TestCatalogs.Fern]),
            ("rocky", [TestCatalogs.RockPile, TestCatalogs.RockBoulder, TestCatalogs.RockCluster]),
        };

        Assert.All(perBand, band => Assert.All(band.Kinds, kind =>
            Assert.True(outThere.GetValueOrDefault(kind) > 0, $"The open world's {band.Band} band grew no '{kind}'.")));
    }

    [Fact]
    public void LoadDefaultLeavesSomeOfTheOpenWorldEmptyInsteadOfThinningEverythingEvenly()
    {
        var map = LoadDefault();

        // The density field is what makes soft-edged clearings and barren stretches emerge at
        // all: only part of the candidate points survive their roll against it. Every candidate
        // surviving would be a uniform sprinkle over the whole terrain instead, and none
        // surviving would leave the open world bare.
        var openWorldNodes = map.World.ResourceNodes.Count(n => WorldState.Distance(n.Position, map.CampCenter) > 200);

        Assert.InRange(openWorldNodes, 1, 15999);
    }

    [Fact]
    public void LoadDefaultStartsWithNoBuildings()
    {
        var map = LoadDefault();

        Assert.Empty(map.World.Buildings);
    }
}
