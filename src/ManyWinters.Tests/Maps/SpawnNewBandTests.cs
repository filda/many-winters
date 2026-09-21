using ManyWinters.Core.Maps;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Maps;

// The terrain patch is 1000 m across; beyond it there is no ground for a person, a stock pile
// or an apple tree to stand on. The old camp can sit anywhere the game has put one, including
// close to the patch's edge.
public class SpawnNewBandTests
{
    // Mirrors the terrain half-extent, which Core keeps private because it is hardcoded rather
    // than read from the Godot content.
    private const double TerrainHalfMeters = 500;

    private const int SeedCount = 5;

    // The starting camp, the far corner of the camp-center safe zone, and camps on the terrain's
    // very edge - the last is where an older clamp to the edge could leave one.
    private static readonly Position[] OldCamps =
    [
        new(5, 250),
        new(484, 484),
        new(-484, 484),
        new(484, -484),
        new(-484, -484),
        new(500, 0),
        new(0, 500),
        new(-500, 0),
        new(0, -500),
        new(500, 500),
        new(-500, 500),
    ];

    private static List<Entity> ResourceNodes(WorldState world) =>
        world.Entities.Where(e => e.Category == EntityCategory.Growable).ToList();

    private static (WorldState World, Position Camp) Spawn(int seed, Position oldCamp)
    {
        var world = new WorldState(TestCatalogs.CreateConfiguration());
        var camp = MapLoader.SpawnNewBand(world, new Random(seed), oldCamp);
        return (world, camp);
    }

    private static void AssertOnTerrain(Position position, string what, Position oldCamp, int seed)
    {
        var onTerrain = position.X >= -TerrainHalfMeters && position.X <= TerrainHalfMeters
            && position.Y >= -TerrainHalfMeters && position.Y <= TerrainHalfMeters;

        Assert.True(onTerrain, $"Seed {seed}, old camp ({oldCamp.X}, {oldCamp.Y}): {what} at ({position.X:0.##}, {position.Y:0.##}) stands off the terrain.");
    }

    // Every person the band brings, and every stock pile and food plant it scatters, must have
    // ground under it - not just the camp center.
    [Fact]
    public void SpawnNewBandKeepsTheWholeBandOnTheTerrainForCampsNearEveryEdge()
    {
        foreach (var oldCamp in OldCamps)
        {
            for (var seed = 1; seed <= SeedCount; seed++)
            {
                var (world, camp) = Spawn(seed, oldCamp);

                AssertOnTerrain(camp, "the camp center", oldCamp, seed);
                Assert.All(world.People, person => AssertOnTerrain(person.Position, person.Name, oldCamp, seed));
                Assert.All(ResourceNodes(world), node => AssertOnTerrain(node.Position, node.Kind.Value, oldCamp, seed));
            }
        }
    }

    // The walk between the camps keeps its 80..250 m span even when the old camp sits where a
    // naive clamp would pin the new camp on top of it.
    [Fact]
    public void SpawnNewBandCampsTheFullWalkAwayFromAnOldCampInTheSafeZone()
    {
        var safeZoneCamps = new[] { new Position(5, 250), new Position(484, 484), new Position(0, 0), new Position(-484, 300) };

        foreach (var oldCamp in safeZoneCamps)
        {
            for (var seed = 1; seed <= SeedCount; seed++)
            {
                var (_, camp) = Spawn(seed, oldCamp);
                var distance = WorldState.Distance(camp, oldCamp);

                Assert.InRange(distance, 80, 250);
            }
        }
    }

    // Even from a camp on the terrain's very edge the successor stays a full walk away; the
    // clamp can only stretch that walk by a hair, never collapse it.
    [Fact]
    public void SpawnNewBandCampsAtLeastTheFullWalkAwayFromAnOldCampOnTheEdge()
    {
        var edgeCamps = new[] { new Position(500, 0), new Position(0, -500), new Position(500, 500), new Position(-500, 500) };

        foreach (var oldCamp in edgeCamps)
        {
            for (var seed = 1; seed <= SeedCount; seed++)
            {
                var (_, camp) = Spawn(seed, oldCamp);

                Assert.True(WorldState.Distance(camp, oldCamp) >= 80, $"Seed {seed}: the new camp is {WorldState.Distance(camp, oldCamp):0.0} m from the old camp at ({oldCamp.X}, {oldCamp.Y}).");
            }
        }
    }

    // A successor band is a full band, not just a camp centre on a map: the fifteen-person
    // crowd, the hand-placed stock and the camp food all come with it.
    [Fact]
    public void SpawnNewBandSpawnsTheCrowdTheStockAndTheCampFood()
    {
        var (world, camp) = Spawn(1, new Position(5, 250));

        Assert.Equal(15, world.People.Count);

        var nodes = ResourceNodes(world).ToList();
        Assert.Equal(10, nodes.Count);
        Assert.Equal(300f, Assert.Single(nodes, n => n.Kind == TestCatalogs.Wood).Growth!.RemainingAmount);
        Assert.Equal(200f, Assert.Single(nodes, n => n.Kind == TestCatalogs.Grass).Growth!.RemainingAmount);
        foreach (var kind in new[] { TestCatalogs.Apple, TestCatalogs.Pear, TestCatalogs.Mushroom, TestCatalogs.Potato })
        {
            Assert.Equal(2, nodes.Count(n => n.Kind == kind));
        }

        // The stock sits where band-spawning puts it, 5 m north and 10 m east of the camp centre.
        var positions = nodes.Select(n => n.Position).ToList();
        Assert.Contains(new Position(camp.X, camp.Y + 5f), positions);
        Assert.Contains(new Position(camp.X + 10f, camp.Y), positions);
    }

    // Like the first band's forebears, the successor's died a full life before the story began.
    [Fact]
    public void SpawnNewBandGivesForebearsThatDiedOfOldAgeBeforeTheStoryBegan()
    {
        var (world, _) = Spawn(1, new Position(5, 250));
        var rules = world.Configuration.Rules;

        Assert.Equal(18, world.Forebears.Count);
        Assert.All(world.Forebears, forebear =>
        {
            Assert.Equal(-rules.TicksPerYear, forebear.DeathTick);
            Assert.Equal(DeathCause.OldAge, forebear.CauseOfDeath);
        });
    }

    // With the old camp in the open no clamp is in play, so the new camp is exactly the drawn
    // walk from it. The draws mirror the actual order (angle, then distance); a mirrored or
    // divided walk lands somewhere else entirely.
    [Fact]
    public void SpawnNewBandPlacesTheCampAtTheDrawnWalkFromTheOldCamp()
    {
        const int seed = 7;
        var oldCamp = new Position(0, 0);
        var draws = new Random(seed);
        var angle = draws.NextDouble() * Math.Tau;
        var distance = 80 + (draws.NextDouble() * 170);
        var expected = new Position(distance * Math.Cos(angle), distance * Math.Sin(angle));

        var (_, camp) = Spawn(seed, oldCamp);

        Assert.Equal(expected.X, camp.X, 6);
        Assert.Equal(expected.Y, camp.Y, 6);
    }

    [Fact]
    public void SpawnNewBandIsDeterministicForTheSameSeedAndOldCamp()
    {
        var (firstWorld, firstCamp) = Spawn(3, new Position(5, 250));
        var (secondWorld, secondCamp) = Spawn(3, new Position(5, 250));

        Assert.Equal(firstCamp, secondCamp);
        Assert.Equal(firstWorld.People.Select(p => p.Position), secondWorld.People.Select(p => p.Position));
        Assert.Equal(ResourceNodes(firstWorld).Select(n => (n.Kind, n.Position)), ResourceNodes(secondWorld).Select(n => (n.Kind, n.Position)));
    }
}
