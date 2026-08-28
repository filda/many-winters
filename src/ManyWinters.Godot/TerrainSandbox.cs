using Godot;

namespace ManyWinters.Godot;

// Visual/camera prototype sandbox (docs/terrain-and-world-scale-architecture.md, "first
// implementable slice"). Deliberately not wired into Main.cs/WorldState - this only proves
// out real elevation data + camera behavior, per conventions.md's prototype/production split.
public partial class TerrainSandbox : Node3D
{
    private const string HeightmapPath = "res://Content/terrain/praha-liben/heightmap.json";
    private const string WaterwaysPath = "res://Content/terrain/praha-liben/waterways.json";
    private const string GroundTexturePath = "res://Content/terrain/ground.png";

    private const string ConiferTreePath = "res://Content/terrain/conifer_tree.png";
    private const string DeciduousTreePath = "res://Content/terrain/deciduous_tree.png";
    private const string BushPath = "res://Content/terrain/bush.png";
    private const string GrassPath = "res://Content/terrain/grass.png";
    private const string FlowerPath = "res://Content/terrain/flower.png";
    private const string RockPilePath = "res://Content/terrain/rock_pile.png";
    private const string PersonTexturePath = "res://Content/people/person.png";
    private const int TreeCount = 90;
    private const int DeciduousTreeCount = 60;
    private const int BushCount = 50;
    private const int GrassCount = 150;
    private const int FlowerCount = 40;
    private const int RockCount = 35;
    private const int PersonCount = 12;
    private const float TreeHeightMeters = 8f;
    private const float DeciduousTreeHeightMeters = 7f;
    private const float BushHeightMeters = 1.2f;
    private const float GrassHeightMeters = 0.4f;
    private const float FlowerHeightMeters = 0.35f;
    private const float RockHeightMeters = 1.5f;
    private const float PersonHeightMeters = PersonView.Height;
    private const float PropMinScale = 0.8f;
    private const float PropMaxScale = 1.3f;
    private const int PropScatterSeed = 1;

    // Sized for this heightmap's real scale (1000 m across, ~80 m of relief) - not the tiny
    // 20-unit test map Main.cs uses. A camera at the old (test-map-sized) distances would sit
    // inside the terrain itself here.
    private const float InitialZoomDistance = 700f;
    private const float MinZoom = 3f;
    private const float MaxZoom = 2000f;

    private static readonly Color TreeFallbackColor = new(0.20f, 0.32f, 0.18f);
    private static readonly Color DeciduousTreeFallbackColor = new(0.30f, 0.38f, 0.22f);
    private static readonly Color BushFallbackColor = new(0.26f, 0.36f, 0.18f);
    private static readonly Color GrassFallbackColor = new(0.32f, 0.42f, 0.18f);
    private static readonly Color FlowerFallbackColor = new(0.82f, 0.52f, 0.62f);
    private static readonly Color RockFallbackColor = new(0.5f, 0.5f, 0.52f);
    private static readonly Color PersonFallbackColor = new(0.9f, 0.7f, 0.5f);

    private TerrainRenderer _terrain = null!;
    private FreeCameraRig _cameraRig = null!;

    public override void _Ready()
    {
        SetUpLighting();

        _terrain = new TerrainRenderer(HeightmapPath, WaterwaysPath, GroundTexturePath);
        _terrain.BuildTerrainMesh(this);
        _terrain.BuildWaterways(this);

        // Scattered across the whole loaded patch (radius = _terrain.Half), unlike Main.cs's
        // camp-centered radius - this scene is specifically for eyeballing decoration over
        // real terrain at full scale, not a small playable area around a camp.
        var rng = new Random(PropScatterSeed);
        _terrain.ScatterDecoration(this, rng, TreeCount, ConiferTreePath, TreeHeightMeters, TreeFallbackColor, PropMinScale, PropMaxScale, 0f, 0f, _terrain.Half);
        _terrain.ScatterDecoration(this, rng, DeciduousTreeCount, DeciduousTreePath, DeciduousTreeHeightMeters, DeciduousTreeFallbackColor, PropMinScale, PropMaxScale, 0f, 0f, _terrain.Half);
        _terrain.ScatterDecoration(this, rng, BushCount, BushPath, BushHeightMeters, BushFallbackColor, PropMinScale, PropMaxScale, 0f, 0f, _terrain.Half);
        _terrain.ScatterDecoration(this, rng, GrassCount, GrassPath, GrassHeightMeters, GrassFallbackColor, PropMinScale, PropMaxScale, 0f, 0f, _terrain.Half);
        _terrain.ScatterDecoration(this, rng, FlowerCount, FlowerPath, FlowerHeightMeters, FlowerFallbackColor, PropMinScale, PropMaxScale, 0f, 0f, _terrain.Half);
        _terrain.ScatterDecoration(this, rng, RockCount, RockPilePath, RockHeightMeters, RockFallbackColor, PropMinScale, PropMaxScale, 0f, 0f, _terrain.Half);
        _terrain.ScatterDecoration(this, rng, PersonCount, PersonTexturePath, PersonHeightMeters, PersonFallbackColor, PropMinScale, PropMaxScale, 0f, 0f, _terrain.Half);

        _cameraRig = new FreeCameraRig(this, Vector3.Zero, InitialZoomDistance, MinZoom, MaxZoom);
    }

    public override void _Process(double delta)
    {
        _cameraRig.HandleInput((float)delta);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.T })
        {
            _cameraRig.ToggleProjection();
        }
    }

    // See Main.cs's _UnhandledInput for why this isn't in _Input, and why it also checks
    // GuiGetHoveredControl (this scene has no UI today, but keeps the same guard for when
    // it does rather than silently losing it if one gets added here later).
    public override void _UnhandledInput(InputEvent @event)
    {
        if (GetViewport().GuiGetHoveredControl() is not null)
        {
            return;
        }

        _cameraRig.HandleMouseInput(@event);
    }

    private void SetUpLighting()
    {
        AddChild(new DirectionalLight3D
        {
            Rotation = new Vector3(Mathf.DegToRad(-55), Mathf.DegToRad(-35), 0),
        });
    }
}
