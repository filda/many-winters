using Godot;

namespace ManyWinters.Godot.Terrain;

// Builds the terrain mesh and waterways - presentation only, no gameplay rule lives here
// (MapLoader defines the starting WorldState). Scattered decorations are ResourceNodes spawned
// by MapLoader.ScatterDecorations and rendered through WorldPresenter, not built here.
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
