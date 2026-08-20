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

    public static LoadedMap LoadDefault(WorldConfiguration configuration)
    {
        var world = new WorldState(configuration);

        const int columns = 5;
        for (var i = 0; i < 15; i++)
        {
            var x = (((i % columns) - (columns / 2f) + 0.5f) * 2f) + CampCenter.X;
            var z = (((i / columns) - 1) * 2f) + CampCenter.Y;
            var initialAgeTicks = StartingAgesInWinters[i] * WorldState.TicksPerYear;
            var motherId = StartingMotherIndex[i] is { } motherIndex ? new PersonId(motherIndex + 1) : (PersonId?)null;
            var fatherId = StartingFatherIndex[i] is { } fatherIndex ? new PersonId(fatherIndex + 1) : (PersonId?)null;
            world.Execute(new SpawnPersonCommand(StartingNames[i], new Position(x, z), initialAgeTicks, motherId, fatherId));
        }

        world.Execute(new SpawnResourceNodeCommand(new ResourceKindId("apple"), Offset(-6f, 5f), 200f));
        world.Execute(new SpawnResourceNodeCommand(new ResourceKindId("pear"), Offset(0f, -5f), 200f));
        world.Execute(new SpawnResourceNodeCommand(new ResourceKindId("mushroom"), Offset(6f, 5f), 200f));
        world.Execute(new SpawnResourceNodeCommand(new ResourceKindId("potato"), Offset(-6f, -5f), 200f));
        world.Execute(new SpawnResourceNodeCommand(new ResourceKindId("apple"), Offset(6f, -5f), 200f));
        world.Execute(new SpawnResourceNodeCommand(new ResourceKindId("wood"), Offset(0f, 5f), 300f));

        return new LoadedMap(world, CampCenter);
    }

    private static Position Offset(double x, double y) => new(CampCenter.X + x, CampCenter.Y + y);
}
