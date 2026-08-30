using ManyWinters.Core.Commands;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Maps;

public static class MapLoader
{
    // North of the real terrain patch's center (docs/terrain-and-world-scale-architecture.md) -
    // the actual Rokytka waterway runs well south of here (see art/fetch_stream.py's output),
    // so the camp sits on dry ground rather than in the middle of a real river.
    private static readonly Position CampCenter = new(5, 250);

    private static readonly string[] StartingNames =
    [
        "Ava", "Bran", "Tora", "Kael", "Mira", "Doran", "Liska", "Faro",
        "Ivy", "Rask", "Sela", "Bodin", "Yara", "Corin", "Vessa",
    ];

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
    private const int WideTreeCount = 70;
    private const int WideDeciduousTreeCount = 50;
    private const int WideBushCount = 40;
    private const int WideGrassCount = 500;
    private const int WideFlowerCount = 120;
    private const int WideFernCount = 300;
    private const int WideRockCount = 30;
    private const int WideStumpCount = 12;
    private const int WideFallenLogCount = 8;
    private const int GroveTreeCount = 85;
    private const int GroveDeciduousTreeCount = 55;
    private const int GroveBushCount = 50;
    private const int GroveGrassCount = 530;
    private const int GroveFlowerCount = 90;
    private const int GroveFernCount = 190;
    private const int GroveRockCount = 30;
    private const int GroveStumpCount = 6;
    private const int GroveFallenLogCount = 4;

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
            world.Execute(new SpawnPersonCommand(StartingNames[i], position, initialAgeTicks, motherId, fatherId));
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

        SpawnKind(ConiferTreeKind, WideTreeCount, WoodAmount, 0, 0, TerrainHalfMeters);
        SpawnKind(DeciduousTreeKind, WideDeciduousTreeCount, WoodAmount, 0, 0, TerrainHalfMeters);
        SpawnKind(BushKind, WideBushCount, WoodAmount, 0, 0, TerrainHalfMeters);
        SpawnKind(GrassKind, WideGrassCount, GroundCoverAmount, 0, 0, TerrainHalfMeters);
        SpawnKind(FlowerKind, WideFlowerCount, GroundCoverAmount, 0, 0, TerrainHalfMeters);
        SpawnKind(FernKind, WideFernCount, GroundCoverAmount, 0, 0, TerrainHalfMeters);
        SpawnRock(WideRockCount, 0, 0, TerrainHalfMeters);
        SpawnKind(TreeStumpKind, WideStumpCount, DeadWoodAmount, 0, 0, TerrainHalfMeters);
        SpawnKind(FallenLogKind, WideFallenLogCount, DeadWoodAmount, 0, 0, TerrainHalfMeters);
    }

    // Same spatial-hash rejection sampling as TerrainRenderer.ScatterDecoration (cell size =
    // MinDecorationSpacing) - independent of that one (different Dictionary instance, per
    // ScatterDecorations call), since these are two entirely separate placement passes now
    // (this one spawns real ResourceNodes; TerrainRenderer's only still serves TerrainSandbox's
    // own preview scatter).
    private static Position NextDecorationPosition(Random rng, Dictionary<(int, int), List<Position>> occupied, double centerX, double centerY, double radius)
    {
        var position = new Position(centerX, centerY);
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

    private static (int, int) CellFor(Position position) =>
        ((int)Math.Floor(position.X / MinDecorationSpacing), (int)Math.Floor(position.Y / MinDecorationSpacing));

    private static bool IsTooCloseToAnExistingDecoration(Dictionary<(int, int), List<Position>> occupied, Position candidate)
    {
        var (cellX, cellY) = CellFor(candidate);
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                if (!occupied.TryGetValue((cellX + dx, cellY + dy), out var positions))
                {
                    continue;
                }

                foreach (var existing in positions)
                {
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
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var candidate = RandomDiskPosition(rng);
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
