using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Maps;

public static class MapLoader
{
    // North of the terrain patch's center: the real Rokytka runs well south of here, so the camp
    // sits on dry ground.
    private static readonly Position CampCenter = new(5, 250);

    // Age spread in winters, mostly young/middle with a couple of elders (MaxLifespanYears is
    // 10). Fixed rather than randomized so a new game is the same world twice.
    private static readonly long[] StartingAgesInWinters = [2, 4, 8, 1, 5, 3, 9, 2, 6, 1, 4, 7, 2, 3, 5];

    // 0-based indices into the arrays above of each starting person's mother/father; null means
    // no recorded parent. Only the starting crowd gets family this way - every later tie comes
    // from BirthCommand.
    private static readonly int?[] StartingMotherIndex =
        [10, null, null, null, 2, 8, null, 10, null, null, null, null, null, 8, 2];

    private static readonly int?[] StartingFatherIndex =
        [1, null, null, null, 6, 11, null, 1, null, null, null, null, null, 11, 6];

    // Disk-uniform scatter with minimum spacing reads as a loosely gathered crowd; a grid reads
    // as soldiers on parade. Seeded for reproducibility.
    private const int CrowdPlacementSeed = 1;

    // Entities normally draw their own random id (see EntityId); the starting map draws them
    // from this one seeded generator so a new game is the same world twice, down to every
    // variant keyed off an id's seed. Separate from the placement generators: 16 bytes per
    // entity drawn from those would shift every position that follows.
    private const int EntityIdSeed = 3;
    private const float CrowdRadius = 4f;
    private const float CrowdMinSpacing = 1f;

    // Half the terrain patch's extent (heightmap.json: (gridSize 41 - 1) * cellSizeMeters 25 / 2),
    // hardcoded because Core has no dependency on Godot content.
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
    private const int MushroomCount = 15;
    private const int GroveTreeCount = 85;
    private const int GroveDeciduousTreeCount = 55;
    private const int GroveBushCount = 50;
    private const int GroveGrassCount = 530;
    private const int GroveFlowerCount = 90;
    private const int GroveFernCount = 190;
    private const int GroveRockCount = 30;
    private const int GroveStumpCount = 6;
    private const int GroveFallenLogCount = 4;
    private const int GroveMushroomCount = 6;

    // Wild food growing right where the band settled, scattered over a radius small enough that
    // the starting crowd has food within a short walk - the open world's food
    // (ScatterOpenWorldBiomes) is far too thin to count on in the first winter.
    private const float CampFoodRadius = 12f;
    private const int CampAppleCount = 2;
    private const int CampPearCount = 2;
    private const int CampMushroomCount = 2;
    private const int CampPotatoCount = 2;

    // The open terrain beyond the dense zone and groves samples two coherent noise fields
    // (Noise2D) per candidate point: one decides how likely anything grows there, so soft-edged
    // clearings and barren stretches emerge; the other decides which biome band a surviving
    // point falls into. Neighbouring points sample similar values, so regions cluster on their
    // own without any explicit shape being drawn.
    private const int OpenWorldBiomeNoiseSeed = 7;
    private const int OpenWorldDensityNoiseSeed = 8;
    private const double BiomeNoiseFrequency = 1.0 / 220.0;
    private const double DensityNoiseFrequency = 1.0 / 140.0;
    private const int OpenWorldCandidateCount = 16000;

    // Thresholds over the biome noise's [0, 1] range. Forest is the rarest band (the dense zone
    // and groves already supply plenty); rocky, the most common, is everything below MeadowBandMin.
    private const double ForestBandMin = 0.72;
    private const double ThicketBandMin = 0.56;
    private const double MeadowBandMin = 0.38;

    // Renewable kinds (RegenPerTick > 0) get more than the finite ones (rock, stump, log), which
    // never come back once spent.
    private const float WoodAmount = 200f;
    private const float FoodAmount = 200f;
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
    private static readonly ResourceKindId AppleKind = new("apple");
    private static readonly ResourceKindId PearKind = new("pear");
    private static readonly ResourceKindId MushroomKind = new("mushroom");
    private static readonly ResourceKindId PotatoKind = new("potato");

    private static readonly ResourceKindId[] RockKinds =
    [
        new("rock_pile"), new("rock_boulder"), new("rock_cluster"),
    ];

    public static LoadedMap LoadDefault(WorldConfiguration configuration)
    {
        var world = new WorldState(configuration);
        var idRng = new Random(EntityIdSeed);

        SpawnBand(world, idRng, CampCenter, -world.Configuration.Rules.TicksPerYear);

        ScatterDecorations(world, idRng);

        return new LoadedMap(world, CampCenter);
    }

    // Spawns a successor band into an existing world. Picks a new camp 80..250 m from the old one,
    // spawns the crowd with starting stock and food, and returns the camp center for the caller
    // to move the camera.
    public static Position SpawnNewBand(WorldState world, Random idRng, Position oldCampCenter)
    {
        var angle = idRng.NextDouble() * Math.Tau;
        var distance = 80 + (idRng.NextDouble() * 170);
        var campCenter = ClampToTerrain(
            oldCampCenter.X + (Math.Cos(angle) * distance),
            oldCampCenter.Y + (Math.Sin(angle) * distance));

        SpawnBand(world, idRng, campCenter, world.Clock.CurrentTick - world.Configuration.Rules.TicksPerYear);
        SpawnCampFood(world, new Random(idRng.Next()), idRng, campCenter);

        return campCenter;
    }

    private static Position ClampToTerrain(double x, double y) =>
        new(
            Math.Max(-TerrainHalfMeters, Math.Min(TerrainHalfMeters, x)),
            Math.Max(-TerrainHalfMeters, Math.Min(TerrainHalfMeters, y)));

    private static void SpawnCampFood(WorldState world, Random rng, Random idRng, Position campCenter)
    {
        Position RandomCampPosition(double x, double y) =>
            new(campCenter.X + x, campCenter.Y + y);

        void Spawn(ResourceKindId kind, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var angle = rng.NextDouble() * Math.Tau;
                var distance = CampFoodRadius * Math.Sqrt(rng.NextDouble());
                var position = RandomCampPosition(Math.Cos(angle) * distance, Math.Sin(angle) * distance);
                world.Execute(new SpawnResourceNodeCommand(ResourceNodeId.New(idRng), kind, position, FoodAmount));
            }
        }

        Spawn(AppleKind, CampAppleCount);
        Spawn(PearKind, CampPearCount);
        Spawn(MushroomKind, CampMushroomCount);
        Spawn(PotatoKind, CampPotatoCount);
    }

    private static Position NextCrowdPositionFor(Random rng, List<Position> placed, Position center) =>
        FreePositionSearch.Find(
            () => RandomDiskPositionFor(rng, center),
            candidate => placed.All(p => WorldState.Distance(p, candidate) >= CrowdMinSpacing),
            maxAttempts: 30);

    private static Position RandomDiskPositionFor(Random rng, Position center)
    {
        var angle = rng.NextDouble() * Math.Tau;
        var distance = CrowdRadius * Math.Sqrt(rng.NextDouble());
        return new Position(center.X + (distance * Math.Cos(angle)), center.Y + (distance * Math.Sin(angle)));
    }

    // Spawns a band of 15 people with family ties and forebears, plus starting stock (wood and
    // grass). Camp food (fruit, roots, mushrooms) is scattered separately: ScatterDecorations
    // for a fresh world, SpawnCampFood for a successor band into an existing one.
    private static void SpawnBand(WorldState world, Random idRng, Position campCenter, long forebearDeathTick)
    {
        var rules = world.Configuration.Rules;
        var rng = new Random(CrowdPlacementSeed);
        var positions = new List<Position>();

        // Stryker disable once Equality: only fills the list; everything below indexes it by the ages array, so a spare entry moves nobody
        for (var i = 0; i < StartingAgesInWinters.Length; i++)
        {
            positions.Add(NextCrowdPositionFor(rng, positions, campCenter));
        }

        var spawned = new Dictionary<int, Person>();
        var nextForebearName = 0;

        Person SpawnForebear(Sex sex)
        {
            // Died before the story began, after a full life: old enough to have raised anyone in
            // the crowd, gone long enough to be buried rather than lying around camp.
            var id = PersonId.New(idRng);
            var forebear = new Person
            {
                Id = id,
                Name = PersonNames.Forebears[nextForebearName++],
                BirthTick = forebearDeathTick - (rules.MaxLifespanYears * rules.TicksPerYear),
                IsAlive = false,
                DeathTick = forebearDeathTick,
                CauseOfDeath = DeathCause.OldAge,
                IsBuried = true,
                Mother = Person.Unknown,
                Father = Person.Unknown,
                Sex = sex,
                MaxHunger = rules.MaxHungerFor(id),
            };

            world.AddForebear(forebear);
            return forebear;
        }

        Person SpawnStarting(int index)
        {
            if (spawned.TryGetValue(index, out var alreadySpawned))
            {
                return alreadySpawned;
            }

            var mother = StartingMotherIndex[index] is { } motherIndex ? SpawnStarting(motherIndex) : SpawnForebear(Sex.Female);
            var father = StartingFatherIndex[index] is { } fatherIndex ? SpawnStarting(fatherIndex) : SpawnForebear(Sex.Male);
            var initialAgeTicks = StartingAgesInWinters[index] * rules.TicksPerYear;
            world.Execute(new SpawnPersonCommand(
                PersonId.New(idRng),
                PersonNames.Pool[index],
                positions[index],
                mother,
                father,
                initialAgeTicks,
                StartingSexFor(index)));

            // Commands are plain data (ICommand) and return nothing; the person just added is the
            // newest in People.
            var person = world.People[^1];
            spawned[index] = person;
            return person;
        }

        for (var i = 0; i < StartingAgesInWinters.Length; i++)
        {
            SpawnStarting(i);
        }

        // The band's starting stock, not scenery. Everything that grows — food included — is
        // scattered by ScatterDecorations (fresh world) or SpawnCampFood (successor band).
        world.Execute(new SpawnResourceNodeCommand(ResourceNodeId.New(idRng), new ResourceKindId("wood"), new Position(campCenter.X, campCenter.Y + 5f), 300f));
        world.Execute(new SpawnResourceNodeCommand(ResourceNodeId.New(idRng), new ResourceKindId("grass"), new Position(campCenter.X + 10f, campCenter.Y), 200f));
    }

    // Anyone the family table names as a mother or father has their sex settled by it, not by
    // their id (see Person.Sex), or the table could hand a man a child to have borne.
    private static Sex? StartingSexFor(int index)
    {
        if (StartingMotherIndex.Contains(index))
        {
            return Sex.Female;
        }

        if (StartingFatherIndex.Contains(index))
        {
            return Sex.Male;
        }

        return null;
    }

    // Spawns the scattered decoration - trees, bushes, ground cover, rocks, stumps, logs - as
    // real gatherable ResourceNodes: a dense zone around camp, several groves, then the open world.
    private static void ScatterDecorations(WorldState world, Random idRng)
    {
        var rng = new Random(DecorationScatterSeed);
        var occupied = new SpatialSpacingIndex<Position>(MinDecorationSpacing, p => p.X, p => p.Y);

        void SpawnKind(ResourceKindId kind, int count, float amount, double centerX, double centerY, double radius)
        {
            for (var i = 0; i < count; i++)
            {
                var position = NextDecorationPosition(rng, occupied, centerX, centerY, radius);
                world.Execute(new SpawnResourceNodeCommand(ResourceNodeId.New(idRng), kind, position, amount));
            }
        }

        void SpawnRock(int count, double centerX, double centerY, double radius)
        {
            for (var i = 0; i < count; i++)
            {
                var position = NextDecorationPosition(rng, occupied, centerX, centerY, radius);
                var kind = RockKinds[rng.Next(RockKinds.Length)];
                world.Execute(new SpawnResourceNodeCommand(ResourceNodeId.New(idRng), kind, position, RockAmount));
            }
        }

        // Several jittered, overlapping sub-disks instead of one perfect circle, so a forest has
        // an irregular outline. Ground cover isn't part of the clump; it scatters once over the
        // whole disk, right after.
        void ScatterClump(
            double centerX, double centerY, float radius, int subClusters,
            int treeCount, int deciduousCount, int bushCount, int rockCount, int stumpCount, int fallenLogCount, int mushroomCount)
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

                // Mushrooms grow in the shade of a real stand of trees, not in the open world's
                // forest band, which is too rare to grow more than a few dozen trees.
                SpawnKind(MushroomKind, mushroomCount / subClusters, FoodAmount, subX, subY, subRadius);
            }
        }

        // Before the dense zone, so camp's food gets the open ground closest to the crowd.
        SpawnKind(AppleKind, CampAppleCount, FoodAmount, CampCenter.X, CampCenter.Y, CampFoodRadius);
        SpawnKind(PearKind, CampPearCount, FoodAmount, CampCenter.X, CampCenter.Y, CampFoodRadius);
        SpawnKind(MushroomKind, CampMushroomCount, FoodAmount, CampCenter.X, CampCenter.Y, CampFoodRadius);
        SpawnKind(PotatoKind, CampPotatoCount, FoodAmount, CampCenter.X, CampCenter.Y, CampFoodRadius);

        ScatterClump(CampCenter.X, CampCenter.Y, DecorationRadius, DenseZoneSubClusters, TreeCount, DeciduousTreeCount, BushCount, RockCount, StumpCount, FallenLogCount, MushroomCount);
        SpawnKind(GrassKind, GrassCount, GroundCoverAmount, CampCenter.X, CampCenter.Y, DecorationRadius);
        SpawnKind(FlowerKind, FlowerCount, GroundCoverAmount, CampCenter.X, CampCenter.Y, DecorationRadius);
        SpawnKind(FernKind, FernCount, GroundCoverAmount, CampCenter.X, CampCenter.Y, DecorationRadius);

        for (var i = 0; i < GroveCount; i++)
        {
            var groveX = (rng.NextDouble() - 0.5) * 2 * TerrainHalfMeters;
            var groveY = (rng.NextDouble() - 0.5) * 2 * TerrainHalfMeters;
            ScatterClump(
                groveX, groveY, GroveRadius, GroveSubClusters,
                GroveTreeCount, GroveDeciduousTreeCount, GroveBushCount, GroveRockCount, GroveStumpCount, GroveFallenLogCount, GroveMushroomCount);
            SpawnKind(GrassKind, GroveGrassCount, GroundCoverAmount, groveX, groveY, GroveRadius);
            SpawnKind(FlowerKind, GroveFlowerCount, GroundCoverAmount, groveX, groveY, GroveRadius);
            SpawnKind(FernKind, GroveFernCount, GroundCoverAmount, groveX, groveY, GroveRadius);
        }

        ScatterOpenWorldBiomes(world, rng, idRng, occupied);
    }

    // Approach described at OpenWorldBiomeNoiseSeed. Each candidate is one independent (x, y)
    // sample, not a cluster center: the noise fields alone decide whether it survives and what
    // grows there, so any clustering is the noise's own spatial coherence.
    private static void ScatterOpenWorldBiomes(WorldState world, Random rng, Random idRng, SpatialSpacingIndex<Position> occupied)
    {
        var densityNoise = new Noise2D(OpenWorldDensityNoiseSeed);
        var biomeNoise = new Noise2D(OpenWorldBiomeNoiseSeed);

        // Two bands grow a little food so the open world is worth foraging: wild fruit trees in
        // the thicket, roots in the meadow (mushrooms come with ScatterClump). Both are rare tails
        // on the roll - the food around camp is what the first winter runs on.

        // Stryker disable Equality: every threshold here is compared against a continuous
        // NextDouble(), which lands exactly on one with probability zero, so < and <= agree
        (ResourceKindId Kind, float Amount) PickMeadowKind()
        {
            var roll = rng.NextDouble();
            if (roll < 0.55)
            {
                return (GrassKind, GroundCoverAmount);
            }

            if (roll < 0.8)
            {
                return (FlowerKind, GroundCoverAmount);
            }

            return roll < 0.99 ? (FernKind, GroundCoverAmount) : (PotatoKind, FoodAmount);
        }

        // Stryker disable once Equality: continuous draw, as the disabled block above - which does not reach into a local function's body
        (ResourceKindId Kind, float Amount) PickForestKind() =>
            rng.NextDouble() < 0.55 ? (ConiferTreeKind, WoodAmount) : (DeciduousTreeKind, WoodAmount);

        (ResourceKindId Kind, float Amount) PickThicketKind()
        {
            var roll = rng.NextDouble();
            if (roll < 0.6)
            {
                return (BushKind, WoodAmount);
            }

            if (roll < 0.94)
            {
                return (FernKind, GroundCoverAmount);
            }

            // A separate coin flip, so a stand of wild fruit comes out mixed rather than all apple.
            return rng.NextDouble() < 0.5 ? (AppleKind, FoodAmount) : (PearKind, FoodAmount);
        }

        // Stryker restore Equality

        // Stryker disable once Equality: a sampling budget, not a quantity - one more roll against the same density field
        for (var i = 0; i < OpenWorldCandidateCount; i++)
        {
            var x = (rng.NextDouble() - 0.5) * 2 * TerrainHalfMeters;
            var y = (rng.NextDouble() - 0.5) * 2 * TerrainHalfMeters;

            // A roll against the density field, not a hard threshold, so region edges fade out.
            var density = densityNoise.Fbm(x, y, 3, DensityNoiseFrequency);

            // Stryker disable once Equality: a draw landing exactly on the density value has
            // probability zero, so > and >= reject the same points
            if (rng.NextDouble() > density)
            {
                continue;
            }

            var position = new Position(x, y);
            if (occupied.IsTooClose(position.X, position.Y, _ => MinDecorationSpacing))
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

            world.Execute(new SpawnResourceNodeCommand(ResourceNodeId.New(idRng), kind, position, amount));
            occupied.Add(position);
        }
    }

    // Rejection sampling over a shared SpatialSpacingIndex, one instance per ScatterDecorations
    // pass. TerrainRenderer keeps its own copy of this sampling for TerrainSandbox's preview only.
    private static Position NextDecorationPosition(Random rng, SpatialSpacingIndex<Position> occupied, double centerX, double centerY, double radius)
    {
        var position = new Position(centerX, centerY);

        // Stryker disable once Equality,Update: the attempt cap is a give-up guard that a free spot
        // always turns up well before, so its value or the counter moving changes nothing placed
        for (var attempt = 0; attempt < MaxDecorationPlacementAttempts; attempt++)
        {
            var angle = rng.NextDouble() * Math.Tau;
            var distance = radius * Math.Sqrt(rng.NextDouble());
            position = new Position(centerX + (Math.Cos(angle) * distance), centerY + (Math.Sin(angle) * distance));
            if (!occupied.IsTooClose(position.X, position.Y, _ => MinDecorationSpacing))
            {
                break;
            }
        }

        occupied.Add(position);
        return position;
    }
}
