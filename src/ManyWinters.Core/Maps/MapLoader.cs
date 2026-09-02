using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Maps;

public static class MapLoader
{
    // North of the real terrain patch's center (docs/terrain-and-world-scale-architecture.md) -
    // the actual Rokytka waterway runs well south of here (see art/fetch_stream.py's output),
    // so the camp sits on dry ground rather than in the middle of a real river.
    private static readonly Position CampCenter = new(5, 250);

    // A small band's age spread (winters) rather than fifteen newborns or fifteen near-identical
    // ages - mostly young/middle, with a couple of elders (MaxLifespanYears is 10). Fixed, not
    // randomized, for the same determinism reason as EntityVisualVariation's seeding.
    private static readonly long[] StartingAgesInWinters = [2, 4, 8, 1, 5, 3, 9, 2, 6, 1, 4, 7, 2, 3, 5];

    // Indices (0-based, into the arrays above) of each starting person's mother/father, or null
    // for someone with no recorded parent. Three couples with children, plus a few people with no
    // recorded family - basic family relationships. There's no reproduction command yet, so this
    // is currently the only way any family tie can exist.
    private static readonly int?[] StartingMotherIndex =
        [10, null, null, null, 2, 8, null, 10, null, null, null, null, null, 8, 2];

    private static readonly int?[] StartingFatherIndex =
        [1, null, null, null, 6, 11, null, 1, null, null, null, null, null, 11, 6];

    // A grid reads as soldiers on parade, not a family standing around camp - disk-uniform
    // scatter (same technique as TerrainRenderer.ScatterDecoration) with a minimum-spacing
    // rejection reads as a loosely gathered crowd instead. Seeded, not time-based, for the
    // same reproducibility reason the ages/family ties above are a fixed array rather than
    // randomized.
    private const int CrowdPlacementSeed = 1;
    private const float CrowdRadius = 4f;
    private const float CrowdMinSpacing = 1f;

    // What used to be TerrainSetup.cs's purely-visual scattered decoration (background
    // conifer/deciduous trees, bushes, ground cover, rocks, stumps, fallen logs) is now real,
    // clickable, gatherable ResourceNodes (todo #7: "všechny dekorace... mají časem být
    // skutečné klikatelné ResourceNode") - so this is where they're spawned instead, using the
    // same counts/radii TerrainSetup used to keep the world looking as dense/varied as before.
    // Half the real terrain patch's extent (heightmap.json: gridSize=41, cellSizeMeters=25 ->
    // (41-1)*25/2 = 500) - hardcoded rather than read from the heightmap, since MapLoader
    // (Core) has no dependency on Godot content; TerrainSetup itself hardcodes its own
    // decoration radii relative to the same value for the same reason.
    private const float TerrainHalfMeters = 500f;
    private const float DecorationRadius = 110f;
    private const float GroveRadius = 65f;
    private const int GroveCount = 6;
    private const int DenseZoneSubClusters = 5;
    private const int GroveSubClusters = 3;
    private const int DecorationScatterSeed = 2;
    private const float MinDecorationSpacing = 0.1f;
    private const int MaxDecorationPlacementAttempts = 10;

    private const int TreeCount = 345;
    private const int DeciduousTreeCount = 247;
    private const int BushCount = 222;
    private const int GrassCount = 1500;
    private const int FlowerCount = 250;
    private const int FernCount = 550;
    private const int RockCount = 150;
    private const int StumpCount = 25;
    private const int FallenLogCount = 18;
    private const int GroveTreeCount = 85;
    private const int GroveDeciduousTreeCount = 55;
    private const int GroveBushCount = 50;
    private const int GroveGrassCount = 530;
    private const int GroveFlowerCount = 90;
    private const int GroveFernCount = 190;
    private const int GroveRockCount = 30;
    private const int GroveStumpCount = 6;
    private const int GroveFallenLogCount = 4;

    // Beyond the dense zone and the (still-forest-shaped) groves above, the rest of the
    // terrain used to get only a very thin, uniform "wide pass" - at those counts spread
    // across the whole terrain radius, that read as basically empty most places. A first
    // fix scattered a couple dozen hand-picked circular patches (a meadow disk here, a
    // rocky disk there) instead - visibly better, but still "randomly placed circles", not
    // organic (todo #21's own follow-up). This instead samples two independent, coherent
    // noise fields (see Noise2D) per candidate point across the whole open terrain: one
    // decides how likely anything grows there at all, so genuine soft-edged clearings and
    // barren stretches emerge instead of just "less of everything everywhere"; the other
    // decides which biome band a surviving point falls into. Neighboring points naturally
    // sample similar noise values, so they cluster into soft, irregularly-shaped regions of
    // the same kind on their own - no explicit "draw a circle here" step at all.
    private const int OpenWorldBiomeNoiseSeed = 7;
    private const int OpenWorldDensityNoiseSeed = 8;
    private const double BiomeNoiseFrequency = 1.0 / 220.0;
    private const double DensityNoiseFrequency = 1.0 / 140.0;
    private const int OpenWorldCandidateCount = 16000;

    // Band thresholds over the biome noise's [0, 1] range. Forest is the rarest/densest
    // band (there's already plenty of forest from the dense zone/groves above; the open
    // world's own forest patches are a bonus, not the main event) - rocky, the most common,
    // is everything below MeadowBandMin.
    private const double ForestBandMin = 0.72;
    private const double ThicketBandMin = 0.56;
    private const double MeadowBandMin = 0.38;

    // Renewable ground cover/canopy (regenPerTick > 0) gets an amount in line with the
    // existing hand-placed fruit trees/wood pile below; the finite ones (rock/stump/log,
    // regenPerTick = 0) get a smaller one-shot amount since they never come back once spent.
    private const float WoodAmount = 200f;
    private const float GroundCoverAmount = 100f;
    private const float RockAmount = 80f;
    private const float DeadWoodAmount = 60f;

    private static readonly ResourceKindId ConiferTreeKind = new("conifer_tree");
    private static readonly ResourceKindId DeciduousTreeKind = new("deciduous_tree");
    private static readonly ResourceKindId BushKind = new("bush");
    private static readonly ResourceKindId GrassKind = new("grass");
    private static readonly ResourceKindId FlowerKind = new("flower");
    private static readonly ResourceKindId FernKind = new("fern");
    private static readonly ResourceKindId TreeStumpKind = new("tree_stump");
    private static readonly ResourceKindId FallenLogKind = new("fallen_log");

    private static readonly ResourceKindId[] RockKinds =
    [
        new("rock_pile"), new("rock_boulder"), new("rock_cluster"),
    ];

    public static LoadedMap LoadDefault(WorldConfiguration configuration)
    {
        var world = new WorldState(configuration);

        var rng = new Random(CrowdPlacementSeed);
        var placedPositions = new List<Position>();
        for (var i = 0; i < 15; i++)
        {
            var position = NextCrowdPosition(rng, placedPositions);
            placedPositions.Add(position);
            var initialAgeTicks = StartingAgesInWinters[i] * WorldState.TicksPerYear;
            var motherId = StartingMotherIndex[i] is { } motherIndex ? new PersonId(motherIndex + 1) : (PersonId?)null;
            var fatherId = StartingFatherIndex[i] is { } fatherIndex ? new PersonId(fatherIndex + 1) : (PersonId?)null;
            world.Execute(new SpawnPersonCommand(PersonNames.Pool[i], position, initialAgeTicks, motherId, fatherId));
        }

        world.Execute(new SpawnResourceNodeCommand(new ResourceKindId("apple"), Offset(-6f, 5f), 200f));
        world.Execute(new SpawnResourceNodeCommand(new ResourceKindId("pear"), Offset(0f, -5f), 200f));
        world.Execute(new SpawnResourceNodeCommand(new ResourceKindId("mushroom"), Offset(6f, 5f), 200f));
        world.Execute(new SpawnResourceNodeCommand(new ResourceKindId("potato"), Offset(-6f, -5f), 200f));
        world.Execute(new SpawnResourceNodeCommand(new ResourceKindId("apple"), Offset(6f, -5f), 200f));
        world.Execute(new SpawnResourceNodeCommand(new ResourceKindId("wood"), Offset(0f, 5f), 300f));
        world.Execute(new SpawnResourceNodeCommand(new ResourceKindId("grass"), Offset(10f, 0f), 200f));

        ScatterDecorations(world);

        return new LoadedMap(world, CampCenter);
    }

    private static Position Offset(double x, double y) => new(CampCenter.X + x, CampCenter.Y + y);

    // Ported from TerrainSetup.cs's dense-zone/wide-pass/grove decoration scatter, which used
    // to spawn purely-visual sprites - now spawns real ResourceNodes instead, at the same
    // counts/radii, so the world reads exactly as dense/varied as it did as pure decoration.
    private static void ScatterDecorations(WorldState world)
    {
        var rng = new Random(DecorationScatterSeed);
        var occupied = new Dictionary<(int, int), List<Position>>();

        void SpawnKind(ResourceKindId kind, int count, float amount, double centerX, double centerY, double radius)
        {
            for (var i = 0; i < count; i++)
            {
                var position = NextDecorationPosition(rng, occupied, centerX, centerY, radius);
                world.AddResourceNode(kind, position, amount);
            }
        }

        void SpawnRock(int count, double centerX, double centerY, double radius)
        {
            for (var i = 0; i < count; i++)
            {
                var position = NextDecorationPosition(rng, occupied, centerX, centerY, radius);
                var kind = RockKinds[rng.Next(RockKinds.Length)];
                world.AddResourceNode(kind, position, RockAmount);
            }
        }

        // Same clumped-forest shape as TerrainSetup's own ScatterClump: several smaller,
        // jittered, overlapping sub-disks instead of one perfect circle - see that method's
        // doc comment for why. Ground cover isn't part of the clump; it scatters once over
        // the whole disk, called separately right after, same as before.
        void ScatterClump(
            double centerX, double centerY, float radius, int subClusters,
            int treeCount, int deciduousCount, int bushCount, int rockCount, int stumpCount, int fallenLogCount)
        {
            for (var sub = 0; sub < subClusters; sub++)
            {
                var subAngle = rng.NextDouble() * Math.Tau;
                var subCenterOffset = radius * 0.35 * rng.NextDouble();
                var subX = centerX + (Math.Cos(subAngle) * subCenterOffset);
                var subY = centerY + (Math.Sin(subAngle) * subCenterOffset);
                var subRadius = radius * (0.55 + (rng.NextDouble() * 0.35));

                SpawnKind(ConiferTreeKind, treeCount / subClusters, WoodAmount, subX, subY, subRadius);
                SpawnKind(DeciduousTreeKind, deciduousCount / subClusters, WoodAmount, subX, subY, subRadius);
                SpawnKind(BushKind, bushCount / subClusters, WoodAmount, subX, subY, subRadius);
                SpawnRock(rockCount / subClusters, subX, subY, subRadius);
                SpawnKind(TreeStumpKind, stumpCount / subClusters, DeadWoodAmount, subX, subY, subRadius);
                SpawnKind(FallenLogKind, fallenLogCount / subClusters, DeadWoodAmount, subX, subY, subRadius);
            }
        }

        ScatterClump(CampCenter.X, CampCenter.Y, DecorationRadius, DenseZoneSubClusters, TreeCount, DeciduousTreeCount, BushCount, RockCount, StumpCount, FallenLogCount);
        SpawnKind(GrassKind, GrassCount, GroundCoverAmount, CampCenter.X, CampCenter.Y, DecorationRadius);
        SpawnKind(FlowerKind, FlowerCount, GroundCoverAmount, CampCenter.X, CampCenter.Y, DecorationRadius);
        SpawnKind(FernKind, FernCount, GroundCoverAmount, CampCenter.X, CampCenter.Y, DecorationRadius);

        for (var i = 0; i < GroveCount; i++)
        {
            var groveX = (rng.NextDouble() - 0.5) * 2 * TerrainHalfMeters;
            var groveY = (rng.NextDouble() - 0.5) * 2 * TerrainHalfMeters;
            ScatterClump(
                groveX, groveY, GroveRadius, GroveSubClusters,
                GroveTreeCount, GroveDeciduousTreeCount, GroveBushCount, GroveRockCount, GroveStumpCount, GroveFallenLogCount);
            SpawnKind(GrassKind, GroveGrassCount, GroundCoverAmount, groveX, groveY, GroveRadius);
            SpawnKind(FlowerKind, GroveFlowerCount, GroundCoverAmount, groveX, groveY, GroveRadius);
            SpawnKind(FernKind, GroveFernCount, GroundCoverAmount, groveX, groveY, GroveRadius);
        }

        ScatterOpenWorldBiomes(world, rng, occupied);
    }

    // See OpenWorldCandidateCount's own doc comment for the overall approach. Each
    // candidate is one independent (x, y) sample across the whole terrain - not a center
    // point for a cluster - so the two noise fields alone decide both whether it survives
    // and what grows there; any clustering the result shows is the noise's own spatial
    // coherence, not code drawing a shape.
    private static void ScatterOpenWorldBiomes(WorldState world, Random rng, Dictionary<(int, int), List<Position>> occupied)
    {
        var densityNoise = new Noise2D(OpenWorldDensityNoiseSeed);
        var biomeNoise = new Noise2D(OpenWorldBiomeNoiseSeed);

        // Stryker disable Equality: every threshold here is compared against a continuous
        // NextDouble(), which lands exactly on one of them with probability zero - < and <=
        // pick the same kind
        (ResourceKindId Kind, float Amount) PickMeadowKind()
        {
            var roll = rng.NextDouble();
            if (roll < 0.55)
            {
                return (GrassKind, GroundCoverAmount);
            }

            return roll < 0.8 ? (FlowerKind, GroundCoverAmount) : (FernKind, GroundCoverAmount);
        }

        (ResourceKindId Kind, float Amount) PickForestKind() =>
            rng.NextDouble() < 0.55 ? (ConiferTreeKind, WoodAmount) : (DeciduousTreeKind, WoodAmount);

        (ResourceKindId Kind, float Amount) PickThicketKind() =>
            rng.NextDouble() < 0.6 ? (BushKind, WoodAmount) : (FernKind, GroundCoverAmount);

        // Stryker restore Equality

        for (var i = 0; i < OpenWorldCandidateCount; i++)
        {
            var x = (rng.NextDouble() - 0.5) * 2 * TerrainHalfMeters;
            var y = (rng.NextDouble() - 0.5) * 2 * TerrainHalfMeters;

            // A roll against the density field, not a hard threshold - points near a
            // region's edge fade out gradually rather than stopping dead at a boundary.
            var density = densityNoise.Fbm(x, y, 3, DensityNoiseFrequency);

            // Stryker disable once Equality: a draw landing exactly on the density value has
            // probability zero, so > and >= reject the same points
            if (rng.NextDouble() > density)
            {
                continue;
            }

            var position = new Position(x, y);
            if (IsTooCloseToAnExistingDecoration(occupied, position))
            {
                continue;
            }

            var biome = biomeNoise.Fbm(x, y, 3, BiomeNoiseFrequency);

            // Stryker disable Equality: the noise landing exactly on a band edge has
            // probability zero, so >= and > put the same points in the same band
            var (kind, amount) = biome switch
            {
                >= ForestBandMin => PickForestKind(),
                >= ThicketBandMin => PickThicketKind(),
                >= MeadowBandMin => PickMeadowKind(),
                _ => (RockKinds[rng.Next(RockKinds.Length)], RockAmount),
            };

            // Stryker restore Equality

            world.AddResourceNode(kind, position, amount);
            MarkOccupied(occupied, position);
        }
    }

    // Same spatial-hash rejection sampling as TerrainRenderer.ScatterDecoration (cell size =
    // MinDecorationSpacing) - independent of that one (different Dictionary instance, per
    // ScatterDecorations call), since these are two entirely separate placement passes now
    // (this one spawns real ResourceNodes; TerrainRenderer's only still serves TerrainSandbox's
    // own preview scatter).
    private static Position NextDecorationPosition(Random rng, Dictionary<(int, int), List<Position>> occupied, double centerX, double centerY, double radius)
    {
        var position = new Position(centerX, centerY);

        // Stryker disable once Equality,Update: the attempt cap is a give-up guard, and at
        // these densities a free spot always turns up long before it - how many attempts it
        // allows, or whether the counter moves at all, changes nothing that gets placed
        for (var attempt = 0; attempt < MaxDecorationPlacementAttempts; attempt++)
        {
            var angle = rng.NextDouble() * Math.Tau;
            var distance = radius * Math.Sqrt(rng.NextDouble());
            position = new Position(centerX + (Math.Cos(angle) * distance), centerY + (Math.Sin(angle) * distance));
            if (!IsTooCloseToAnExistingDecoration(occupied, position))
            {
                break;
            }
        }

        MarkOccupied(occupied, position);
        return position;
    }

    // Stryker disable once Arithmetic: the cell size is only how finely the hash buckets
    // positions - a coarser one still gathers every neighbour the 3x3 scan below needs, and
    // that scan measures real distances anyway, so the placements come out identical
    private static (int, int) CellFor(Position position) =>
        ((int)Math.Floor(position.X / MinDecorationSpacing), (int)Math.Floor(position.Y / MinDecorationSpacing));

    private static bool IsTooCloseToAnExistingDecoration(Dictionary<(int, int), List<Position>> occupied, Position candidate)
    {
        var (cellX, cellY) = CellFor(candidate);
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                // Stryker disable once Arithmetic: the offsets run symmetrically from -1 to 1,
                // so adding and subtracting them visit the same nine cells
                if (!occupied.TryGetValue((cellX + dx, cellY + dy), out var positions))
                {
                    continue;
                }

                foreach (var existing in positions)
                {
                    // Stryker disable once Equality: two decorations at exactly the spacing has
                    // probability zero, so < and <= reject the same candidates
                    if (WorldState.Distance(existing, candidate) < MinDecorationSpacing)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static void MarkOccupied(Dictionary<(int, int), List<Position>> occupied, Position position)
    {
        var cell = CellFor(position);
        if (!occupied.TryGetValue(cell, out var positions))
        {
            positions = new List<Position>();
            occupied[cell] = positions;
        }

        positions.Add(position);
    }

    // Rejects a candidate too close to an already-placed person, so the crowd doesn't stack
    // two people exactly on top of each other - falls back to the last candidate tried if
    // CrowdRadius genuinely can't fit this many people with CrowdMinSpacing between them,
    // rather than looping forever.
    private static Position NextCrowdPosition(Random rng, List<Position> placed)
    {
        const int maxAttempts = 30;

        // Stryker disable once Equality,Update: as with the decoration scatter, fifteen people
        // in a four-metre disk always fit well inside the attempt budget, so the cap and its
        // counter never decide anything
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var candidate = RandomDiskPosition(rng);

            // Stryker disable once Equality: a candidate landing at exactly CrowdMinSpacing has
            // probability zero, so >= and > accept the same positions
            if (placed.All(p => WorldState.Distance(p, candidate) >= CrowdMinSpacing))
            {
                return candidate;
            }
        }

        return RandomDiskPosition(rng);
    }

    // Uniform over the disk's area, not its bounding square (see TerrainRenderer's own
    // ScatterDecoration for the same math) - sampling angle and radius independently and
    // uniformly would bunch samples near the center instead.
    private static Position RandomDiskPosition(Random rng)
    {
        var angle = rng.NextDouble() * Math.Tau;
        var distance = CrowdRadius * Math.Sqrt(rng.NextDouble());
        return new Position(CampCenter.X + (distance * Math.Cos(angle)), CampCenter.Y + (distance * Math.Sin(angle)));
    }
}
