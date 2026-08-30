using Godot;

namespace ManyWinters.Godot;

// Builds the terrain mesh and waterways - purely presentation/art, no gameplay rule lives
// here (contrast MapLoader, which defines the actual starting WorldState). Every scattered
// decoration prop (background trees, bushes, ground cover, rocks, stumps, fallen logs) used
// to be built here too, as purely-visual sprites with no gameplay identity; they're now real
// ResourceNodes spawned by MapLoader.ScatterDecorations and rendered like any other resource
// node via WorldPresenter's existing ResourceNodeAdded pipeline (todo #7), so this class no
// longer needs to know about any of them (or about the camp's position).
public static class TerrainSetup
{
    private const string HeightmapPath = "res://Content/terrain/praha-liben/heightmap.json";
    private const string WaterwaysPath = "res://Content/terrain/praha-liben/waterways.json";
    private const string GroundTexturePath = "res://Content/terrain/ground.png";

    public static TerrainRenderer Create(Node3D parent, CollisionObject3D.InputEventEventHandler onGroundClicked)
    {
        var terrain = new TerrainRenderer(HeightmapPath, WaterwaysPath, GroundTexturePath);
        var groundBody = terrain.BuildTerrainMesh(parent);
        groundBody.InputEvent += onGroundClicked;
        terrain.BuildWaterways(parent);

        return terrain;
    }
}
