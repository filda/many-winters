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

        return new LoadedMap(world, CampCenter);
    }

    private static Position Offset(double x, double y) => new(CampCenter.X + x, CampCenter.Y + y);

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
