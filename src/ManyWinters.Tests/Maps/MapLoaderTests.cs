using ManyWinters.Core.Maps;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Maps;

public class MapLoaderTests
{
    // MapLoader.SpawnStarting recurses into a person's recorded parent before spawning that
    // person, so World.People ends up in this order of MapLoader.StartingAgesInWinters/
    // StartingMotherIndex/StartingFatherIndex indices - traced from those tables, not from any
    // name. Naming is procedural now (PhoneticNameGenerator), so a test can no longer identify a
    // starting person by a literal name; it identifies them by this fixed spawn position instead.
    private static readonly int[] SpawnOrderOriginalIndex = [10, 1, 0, 2, 3, 6, 4, 8, 11, 5, 7, 9, 12, 13, 14];

    private static LoadedMap LoadDefault() => MapLoader.LoadDefault(TestCatalogs.CreateConfiguration());

    // person[i] in MapLoader's own index space (StartingAgesInWinters etc.), regardless of
    // spawn order.
    private static Person PersonAt(LoadedMap map, int originalIndex) =>
        map.World.People[Array.IndexOf(SpawnOrderOriginalIndex, originalIndex)];

    // The family table settles who bore whom before any id gets a say
    // (MapLoader.StartingSexFor).
    [Fact]
    public void EveryStartingMotherIsAWomanAndEveryStartingFatherIsAMan()
    {
        var map = LoadDefault();
        var everyone = map.World.People.Concat(map.World.Forebears).ToList();

        foreach (var person in everyone)
        {
            if (everyone.Any(child => ReferenceEquals(child.Mother, person)))
            {
                Assert.Equal(Sex.Female, person.Sex);
            }

            if (everyone.Any(child => ReferenceEquals(child.Father, person)))
            {
                Assert.Equal(Sex.Male, person.Sex);
            }
        }
    }

    // Those who are nobody's parent are left to their id; all one sex would mean the pinning
    // swallowed the draw.
    [Fact]
    public void TheStartingBandIsNotAllOneSex()
    {
        var people = LoadDefault().World.People;

        Assert.Contains(people, person => person.Sex == Sex.Female);
        Assert.Contains(people, person => person.Sex == Sex.Male);
    }

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

        Assert.Same(configuration, map.World.Configuration);
    }

    [Fact]
    public void LoadDefaultPopulatesTheWorldWithFifteenPeopleParentsBeforeChildren()
    {
        var map = LoadDefault();
        var people = map.World.People.ToList();

        Assert.Equal(15, people.Count);
        Assert.Equal(SpawnOrderOriginalIndex.Length, people.Select(p => p.Name).Distinct().Count());

        // A parent is always spawned before their child (MapLoader.SpawnStarting recurses into
        // parents first) - checked here against every starting couple's children.
        Assert.True(people.IndexOf(PersonAt(map, 10)) < people.IndexOf(PersonAt(map, 0)));
        Assert.True(people.IndexOf(PersonAt(map, 1)) < people.IndexOf(PersonAt(map, 0)));
        Assert.True(people.IndexOf(PersonAt(map, 2)) < people.IndexOf(PersonAt(map, 14)));
        Assert.True(people.IndexOf(PersonAt(map, 6)) < people.IndexOf(PersonAt(map, 14)));
        Assert.True(people.IndexOf(PersonAt(map, 8)) < people.IndexOf(PersonAt(map, 13)));
        Assert.True(people.IndexOf(PersonAt(map, 11)) < people.IndexOf(PersonAt(map, 13)));
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

        // Not a grid: no two starting people share an X or a Y.
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

        // MapLoader.StartingAgesInWinters, in its own index order (0..14) rather than spawn order.
        var expectedAges = new long[] { 2, 4, 8, 1, 5, 3, 9, 2, 6, 1, 4, 7, 2, 3, 5 };

        Assert.Equal(expectedAges, Enumerable.Range(0, 15).Select(i => map.World.AgeInYears(PersonAt(map, i))));
    }

    [Fact]
    public void LoadDefaultAssignsFamilyTiesForEachOfTheThreeStartingCouplesChildren()
    {
        var map = LoadDefault();

        // MapLoader.StartingMotherIndex/StartingFatherIndex, by original index rather than name.
        Assert.Same(PersonAt(map, 10), PersonAt(map, 0).Mother);
        Assert.Same(PersonAt(map, 1), PersonAt(map, 0).Father);
        Assert.Same(PersonAt(map, 10), PersonAt(map, 7).Mother);
        Assert.Same(PersonAt(map, 1), PersonAt(map, 7).Father);

        Assert.Same(PersonAt(map, 2), PersonAt(map, 4).Mother);
        Assert.Same(PersonAt(map, 6), PersonAt(map, 4).Father);
        Assert.Same(PersonAt(map, 2), PersonAt(map, 14).Mother);
        Assert.Same(PersonAt(map, 6), PersonAt(map, 14).Father);

        Assert.Same(PersonAt(map, 8), PersonAt(map, 5).Mother);
        Assert.Same(PersonAt(map, 11), PersonAt(map, 5).Father);
        Assert.Same(PersonAt(map, 8), PersonAt(map, 13).Mother);
        Assert.Same(PersonAt(map, 11), PersonAt(map, 13).Father);
    }

    [Fact]
    public void LoadDefaultGivesStartingPeopleWithoutRecordedParentsDeadForebearsInsteadOfUnknown()
    {
        var map = LoadDefault();

        // MapLoader.StartingMotherIndex/StartingFatherIndex are both null for these indices.
        foreach (var index in new[] { 3, 9, 12, 10, 1 })
        {
            foreach (var parent in new[] { PersonAt(map, index).Mother, PersonAt(map, index).Father })
            {
                Assert.Contains(parent, map.World.Forebears);
                Assert.DoesNotContain(parent, map.World.People);
                Assert.False(parent.IsAlive);
                Assert.True(parent.IsBuried);
                Assert.Equal(DeathCause.OldAge, parent.CauseOfDeath);
                Assert.Same(Person.Unknown, parent.Mother);
                Assert.Same(Person.Unknown, parent.Father);
            }
        }
    }

    [Fact]
    public void LoadDefaultGivesEveryForebearADistinctNameNeverSharedWithTheLivingCrowd()
    {
        var map = LoadDefault();

        var forebearNames = map.World.Forebears.Select(f => f.Name).ToList();
        Assert.Equal(18, forebearNames.Count);
        Assert.Equal(forebearNames.Count, forebearNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Empty(forebearNames.Intersect(map.World.People.Select(p => p.Name), StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void LoadDefaultForebearsDiedOfOldAgeBeforeTheStoryBegan()
    {
        var map = LoadDefault();
        var rules = map.World.Configuration.Rules;

        Assert.All(map.World.Forebears, forebear =>
        {
            Assert.Equal(-rules.TicksPerYear, forebear.DeathTick);
            Assert.Equal(rules.MaxLifespanYears, map.World.AgeInYearsAt(forebear, forebear.DeathTick!.Value));
        });
    }

    [Fact]
    public void LoadDefaultGivesForebearsAndPeopleDistinctIdsNoneOfThemUnknowns()
    {
        var map = LoadDefault();

        var ids = map.World.People.Concat(map.World.Forebears).Select(p => p.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.DoesNotContain(Person.Unknown.Id, ids);
    }

    [Fact]
    public void LoadDefaultGivesEveryEntityTheSameIdOnEveryNewGame()
    {
        // The starting map names ids from a seeded generator (MapLoader.EntityIdSeed), so
        // everything keyed off an id's seed - tree variant, hairstyle, wander path - is the
        // same world twice.
        var first = LoadDefault().World;
        var second = LoadDefault().World;

        Assert.Equal(first.People.Select(p => p.Id), second.People.Select(p => p.Id));
        Assert.Equal(first.Forebears.Select(p => p.Id), second.Forebears.Select(p => p.Id));
        Assert.Equal(first.ResourceNodes.Select(n => n.Id), second.ResourceNodes.Select(n => n.Id));
    }

    [Fact]
    public void LoadDefaultDrawsIdsFromTheirOwnGeneratorSoTheyNeverCollideEitherWithinOrAcrossKinds()
    {
        var map = LoadDefault();

        var nodeIds = map.World.ResourceNodes.Select(n => n.Id.Value).ToList();
        var personIds = map.World.People.Concat(map.World.Forebears).Select(p => p.Id.Value).ToList();
        Assert.Equal(nodeIds.Count, nodeIds.Distinct().Count());
        Assert.Empty(nodeIds.Intersect(personIds));
    }

    [Fact]
    public void LoadDefaultPopulatesTheWorldWithTheHandPlacedStartingStockFirst()
    {
        var map = LoadDefault();

        // The hand-placed stock the band brought is spawned before ScatterDecorations, so it is
        // always the first two nodes.
        var expectedFirstTwo = new[]
        {
            (TestCatalogs.Wood, new Position(5f, 255f), 300f),
            (TestCatalogs.Grass, new Position(15f, 250f), 200f),
        };

        Assert.Equal(expectedFirstTwo, map.World.ResourceNodes.Take(2).Select(n => (n.Kind, n.Position, n.RemainingAmount)));
    }

    [Fact]
    public void LoadDefaultGrowsEveryKindOfFoodWithinAShortWalkOfCamp()
    {
        var map = LoadDefault();

        // Every kind of food must be reachable without leaving camp; the open world is far too
        // thin to survive a first winter on.
        var foodKinds = new[] { TestCatalogs.Apple, TestCatalogs.Pear, TestCatalogs.Mushroom, TestCatalogs.Potato };

        var nearCamp = map.World.ResourceNodes
            .Where(n => WorldState.Distance(n.Position, map.CampCenter) <= 12)
            .GroupBy(n => n.Kind)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.All(foodKinds, kind => Assert.True(nearCamp.GetValueOrDefault(kind) >= 2, $"Camp grows fewer than two '{kind}' nodes."));
    }

    [Fact]
    public void LoadDefaultScattersEveryDecorationKindAsRealResourceNodes()
    {
        var map = LoadDefault();

        // Each decoration kind is an individually gatherable ResourceNode, in the thousands
        // (dense zone + wide pass + groves).
        var decorationKinds = new[]
        {
            TestCatalogs.ConiferTree, TestCatalogs.DeciduousTree, TestCatalogs.Bush,
            TestCatalogs.Grass, TestCatalogs.Flower, TestCatalogs.Fern,
            TestCatalogs.RockPile, TestCatalogs.RockBoulder, TestCatalogs.RockCluster,
            TestCatalogs.TreeStump, TestCatalogs.FallenLog,
            TestCatalogs.Apple, TestCatalogs.Pear, TestCatalogs.Mushroom, TestCatalogs.Potato,
        };

        var countsByKind = map.World.ResourceNodes
            .Skip(2)
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

        // Sampled, not all-pairs over thousands of nodes; enough to catch a regression in
        // MapLoader's spacing rejection (MinDecorationSpacing).
        var positions = map.World.ResourceNodes.Skip(2).Select(n => n.Position).Take(500).ToList();
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

        // Seeded, so every new game starts the band in the same spots. Six decimals rather than
        // exact: Math.Cos/Sin's last bit is not identical across platforms.
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

        // In MapLoader's own index order (0..14) - positions are drawn per index before anyone is
        // spawned, so who stands where doesn't shift with the parents-first spawn order.
        Assert.Equal(15, map.World.People.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            var position = PersonAt(map, i).Position;
            Assert.Equal(expected[i].Item1, position.X, 6);
            Assert.Equal(expected[i].Item2, position.Y, 6);
        }
    }

    [Fact]
    public void LoadDefaultBuildsAWorldOfTheSameComposition()
    {
        var map = LoadDefault();

        // Every count is decided by MapLoader alone (fixed counts, seeded noise, placement
        // rejection), so a change in generation shows up here as a number. Update deliberately
        // when retuning; a surprise change is a bug.
        var expected = new Dictionary<ResourceKindId, int>
        {
            [TestCatalogs.Grass] = 7262,
            [TestCatalogs.Fern] = 3148,
            [TestCatalogs.Flower] = 2046,
            [TestCatalogs.Bush] = 1645,
            [TestCatalogs.ConiferTree] = 881,
            [TestCatalogs.DeciduousTree] = 585,
            [TestCatalogs.RockCluster] = 564,
            [TestCatalogs.RockBoulder] = 563,
            [TestCatalogs.RockPile] = 546,
            [TestCatalogs.Potato] = 63,
            [TestCatalogs.TreeStump] = 61,
            [TestCatalogs.Mushroom] = 53,
            [TestCatalogs.Apple] = 48,
            [TestCatalogs.Pear] = 41,
            [TestCatalogs.FallenLog] = 33,
            [TestCatalogs.Wood] = 1,
        };

        var actual = map.World.ResourceNodes.GroupBy(n => n.Kind).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(expected.OrderBy(kv => kv.Key.Value, StringComparer.Ordinal), actual.OrderBy(kv => kv.Key.Value, StringComparer.Ordinal));
        Assert.Equal(17540, map.World.ResourceNodes.Count);
    }

    [Fact]
    public void LoadDefaultGivesEachScatteredKindTheYieldThatMatchesIt()
    {
        var map = LoadDefault();

        // Renewable cover and food plants share one amount; finite kinds (rock, stump, log) get
        // a smaller one-shot amount. The hand-placed grass node is the one scattered kind that
        // also exists at a larger amount.
        var amountsByKind = map.World.ResourceNodes
            .GroupBy(n => n.Kind)
            .ToDictionary(g => g.Key, g => g.Select(n => n.RemainingAmount).Distinct().OrderBy(a => a).ToArray());

        var expected = new Dictionary<ResourceKindId, float[]>
        {
            [TestCatalogs.Apple] = [200f],
            [TestCatalogs.Pear] = [200f],
            [TestCatalogs.Mushroom] = [200f],
            [TestCatalogs.Potato] = [200f],
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

        // The open world scatters across the terrain half-extent (500 m); a grove centred at the
        // edge reaches one grove radius (65 m) further. Beyond that a node has no ground under it.
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

        // Beyond the dense zone (110 m) and any grove only the open-world noise pass remains;
        // each of its four bands (forest, thicket, meadow, rocky fallback) must have produced
        // something.
        var outThere = map.World.ResourceNodes
            .Where(n => WorldState.Distance(n.Position, map.CampCenter) > 200)
            .GroupBy(n => n.Kind)
            .ToDictionary(g => g.Key, g => g.Count());

        var perBand = new (string Band, ResourceKindId[] Kinds)[]
        {
            ("forest", [TestCatalogs.ConiferTree, TestCatalogs.DeciduousTree]),
            ("thicket", [TestCatalogs.Bush, TestCatalogs.Fern, TestCatalogs.Apple, TestCatalogs.Pear]),
            ("meadow", [TestCatalogs.Grass, TestCatalogs.Flower, TestCatalogs.Fern, TestCatalogs.Potato]),
            ("rocky", [TestCatalogs.RockPile, TestCatalogs.RockBoulder, TestCatalogs.RockCluster]),
        };

        Assert.All(perBand, band => Assert.All(band.Kinds, kind =>
            Assert.True(outThere.GetValueOrDefault(kind) > 0, $"The open world's {band.Band} band grew no '{kind}'.")));
    }

    [Fact]
    public void LoadDefaultLeavesSomeOfTheOpenWorldEmptyInsteadOfThinningEverythingEvenly()
    {
        var map = LoadDefault();

        // Only part of the candidates survive their roll against the density field: all would
        // be a uniform sprinkle, none would leave the open world bare.
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
