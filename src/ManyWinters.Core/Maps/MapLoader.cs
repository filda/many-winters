using ManyWinters.Core.Commands;
using ManyWinters.Core.Construction;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Maps;

public static class MapLoader
{
    public static LoadedMap LoadDefault(
        ResourceCatalog resourceCatalog,
        SkillCatalog skillCatalog,
        RecipeCatalog recipeCatalog,
        BuildingCatalog buildingCatalog)
    {
        var world = new WorldState(resourceCatalog, skillCatalog, recipeCatalog, buildingCatalog);

        const int columns = 5;
        for (var i = 0; i < 15; i++)
        {
            var x = ((i % columns) - (columns / 2f) + 0.5f) * 2f;
            var z = ((i / columns) - 1) * 2f;
            world.Execute(new SpawnPersonCommand($"Person {i + 1}", new Position(x, z)));
        }

        world.Execute(new SpawnResourceNodeCommand(new ResourceKindId("apple"), new Position(-6f, 5f), 200f));
        world.Execute(new SpawnResourceNodeCommand(new ResourceKindId("pear"), new Position(0f, -5f), 200f));
        world.Execute(new SpawnResourceNodeCommand(new ResourceKindId("mushroom"), new Position(6f, 5f), 200f));
        world.Execute(new SpawnResourceNodeCommand(new ResourceKindId("potato"), new Position(-6f, -5f), 200f));
        world.Execute(new SpawnResourceNodeCommand(new ResourceKindId("apple"), new Position(6f, -5f), 200f));
        world.Execute(new SpawnResourceNodeCommand(new ResourceKindId("wood"), new Position(0f, 8f), 300f));

        return new LoadedMap(world, TerrainWidth: 20f, TerrainDepth: 20f);
    }
}
