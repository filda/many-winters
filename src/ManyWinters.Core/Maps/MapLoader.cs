using ManyWinters.Core.Commands;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Maps;

public static class MapLoader
{
    // North of the real terrain patch's center (docs/terrain-and-world-scale-architecture.md) -
    // the actual Rokytka waterway runs well south of here (see art/fetch_stream.py's output),
    // so the camp sits on dry ground rather than in the middle of a real river.
    private static readonly Position CampCenter = new(5, 250);

    public static LoadedMap LoadDefault(WorldConfiguration configuration)
    {
        var world = new WorldState(configuration);

        const int columns = 5;
        for (var i = 0; i < 15; i++)
        {
            var x = (((i % columns) - (columns / 2f) + 0.5f) * 2f) + CampCenter.X;
            var z = (((i / columns) - 1) * 2f) + CampCenter.Y;
            world.Execute(new SpawnPersonCommand($"Person {i + 1}", new Position(x, z)));
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
