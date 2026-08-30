using Godot;
using ManyWinters.Core.Commands;
using ManyWinters.Core.Construction;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Maps;
using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;

namespace ManyWinters.Godot;

public partial class Main : Node3D
{
    private const double TickIntervalSeconds = 1.0;

    // Renewed every tick the person stays selected (see _Process), so they never wander off
    // mid-attention - only once selection moves on does this window actually run out.
    private const long SelectedPersonIdleGraceTicks = 5;

    // Comfortably inside WorldState.MaxInteractionDistance (2f), but far enough out that a
    // person's own sprite doesn't overlap the resource node's.
    private const float ApproachDistance = 1.2f;

    private const string HeightmapPath = "res://Content/terrain/praha-liben/heightmap.json";
    private const string WaterwaysPath = "res://Content/terrain/praha-liben/waterways.json";
    private const string GroundTexturePath = "res://Content/terrain/ground.png";
    private const string ConiferTreePath = "res://Content/terrain/conifer_tree.png";
    private const string DeciduousTreePath = "res://Content/terrain/deciduous_tree.png";
    private const string BushPath = "res://Content/terrain/bush.png";
    private const string GrassPath = "res://Content/terrain/grass.png";
    private const string FlowerPath = "res://Content/terrain/flower.png";
    private const string FernPath = "res://Content/terrain/fern.png";
    private const string RockPilePath = "res://Content/terrain/rock_pile.png";
    private const string RockBoulderPath = "res://Content/terrain/rock_boulder.png";
    private const string RockClusterPath = "res://Content/terrain/rock_cluster.png";
    private const string TreeStumpPath = "res://Content/terrain/tree_stump.png";
    private const string FallenLogPath = "res://Content/terrain/fallen_log.png";

    // DecorationRadius grew from the original 70m (see below) to cover much more of the map,
    // not just a small disk hugging camp - every dense-zone count below was scaled up by
    // roughly the same area ratio ((110/70)^2 ~ 2.47x) so the forest reads at least as thick
    // as before over that much bigger footprint, not thinned out by spreading the old counts
    // over a wider area.
    private const int TreeCount = 345;
    private const int DeciduousTreeCount = 247;
    private const int BushCount = 222;
    // Ground cover needs to read as more than scattered specks, but every decoration here is
    // still its own Sprite3D + shadow node pair (these are meant to become real, individually
    // clickable ResourceNode entities later - todo #11/#13 - not get merged into an anonymous
    // GPU-instanced batch that would need unpicking again once that happens), so the total
    // count is a real per-frame cost. A first pass at "1/12 m^2" density (a proper carpet)
    // visibly stuttered - this instead only moderately improves on the original 1/38 m^2.
    private const int GrassCount = 1500;
    private const int FlowerCount = 250;
    private const int FernCount = 550;
    private const int RockCount = 150;
    private const int StumpCount = 25;
    private const int FallenLogCount = 18;
    // A second, sparser pass across the whole real terrain patch (radius = _terrain.Half)
    // on top of the camp-centered one above - without it, everywhere outside
    // DecorationRadius of camp is bare, since the dense pass never reaches that far. This
    // pass covers the *entire* ~1km terrain patch, so it's by far the most sensitive to
    // count - a first attempt at bumping ground cover here (6500/1300/3100) was the single
    // biggest contributor to a visible stutter. These are a modest (~4x) increase over the
    // original 120/30/(no fern yet) instead, not an attempt at real meadow density this far out.
    private const int WideTreeCount = 70;
    private const int WideDeciduousTreeCount = 50;
    private const int WideBushCount = 40;
    private const int WideGrassCount = 500;
    private const int WideFlowerCount = 120;
    private const int WideFernCount = 300;
    private const int WideRockCount = 30;
    private const int WideStumpCount = 12;
    private const int WideFallenLogCount = 8;
    // A handful of standalone groves scattered elsewhere on the map, on top of the wide
    // background pass and the camp's own dense zone - a single "dense disk around camp,
    // sparse everywhere else" gradient reads as one unnaturally uniform blob rather than the
    // patchy, clumped-together way real forest cover actually sits on a landscape. Count and
    // radius both grew alongside DecorationRadius, same area-ratio reasoning.
    private const int GroveCount = 6;
    private const float GroveRadius = 65f;
    private const int GroveTreeCount = 85;
    private const int GroveDeciduousTreeCount = 55;
    private const int GroveBushCount = 50;
    // Same moderate density as the dense zone's own GrassCount/etc.
    private const int GroveGrassCount = 530;
    private const int GroveFlowerCount = 90;
    private const int GroveFernCount = 190;
    private const int GroveRockCount = 30;
    private const int GroveStumpCount = 6;
    private const int GroveFallenLogCount = 4;
    private const float TreeHeightMeters = 8f;
    private const float DeciduousTreeHeightMeters = 7f;
    private const float BushHeightMeters = 1.2f;
    private const float GrassHeightMeters = 0.4f;
    private const float FlowerHeightMeters = 0.35f;
    private const float FernHeightMeters = 0.5f;
    private const float RockHeightMeters = 1.5f;
    private const float StumpHeightMeters = 1.1f;
    private const float FallenLogHeightMeters = 1.3f;
    private const float DecorationMinScale = 0.8f;
    private const float DecorationMaxScale = 1.3f;
    private const int DecorationScatterSeed = 1;
    // Centered on the camp, not spread across the whole real terrain patch (Half=500) - see
    // ScatterDecoration's doc comment. Grown from the original 70m so the forest reads as
    // covering a real swath of the map, not just a small disk immediately around the starting
    // band, while still well short of the full 500m extent.
    private const float DecorationRadius = 110f;

    // Extra margin (in meters) added on top of each candidate's own on-screen half-width -
    // roughly the selected person's own half-width, so something has to clear the target's
    // own silhouette, not just its exact center point, to not count as blocking it - and how
    // transparent something fades to once it does.
    private const float OcclusionMargin = 0.3f;
    private const float OcclusionFadedAlpha = 0.25f;

    // A fixed screen-space size/gap, not a 3D world one: the marker used to be a billboarded
    // Sprite3D offset in local space, but a billboard's own on-screen "left/right" is
    // redefined every frame to match whatever the camera's current right vector is (that's
    // what "always face the camera" means) - so a fixed local offset drifted sideways by a
    // different amount depending on which way the camera was currently facing. Projecting a
    // single stable world point (the head) with Camera3D.UnprojectPosition and drawing the
    // marker as a plain 2D UI overlay above it sidesteps that entirely - Godot's own
    // projection handles the camera math, nothing here has to reconstruct it by hand.
    private const string SelectionMarkerTexturePath = "res://Content/people/selection_marker.png";
    private const float SelectionMarkerScreenSize = 28f;
    private const float SelectionMarkerScreenGap = 6f;

    // The starting band spans roughly 8x4 units and is centered exactly on campPosition
    // (see MapLoader.LoadDefault), so a close default zoom lets it fill most of the frame
    // right away rather than reading as a handful of specks in a huge empty field.
    private const float InitialZoomDistance = 10f;
    private const float MinZoom = 3f;
    private const float MaxZoom = 2000f;

    private static readonly Color TreeFallbackColor = new(0.20f, 0.32f, 0.18f);
    private static readonly Color DeciduousTreeFallbackColor = new(0.30f, 0.38f, 0.22f);
    private static readonly Color BushFallbackColor = new(0.26f, 0.36f, 0.18f);
    private static readonly Color GrassFallbackColor = new(0.32f, 0.42f, 0.18f);
    private static readonly Color FlowerFallbackColor = new(0.82f, 0.52f, 0.62f);
    private static readonly Color FernFallbackColor = new(0.28f, 0.40f, 0.20f);
    private static readonly Color RockFallbackColor = new(0.5f, 0.5f, 0.52f);
    private static readonly Color StumpFallbackColor = new(0.36f, 0.24f, 0.14f);
    private static readonly Color FallenLogFallbackColor = new(0.38f, 0.26f, 0.15f);

    private WorldState _world = null!;
    private WorldPresenter _presenter = null!;
    private TerrainRenderer _terrain = null!;
    private FreeCameraRig _cameraRig = null!;
    private Position _campCenter;

    private Label _infoLabel = null!;
    private Label _buildingsLabel = null!;
    private Label _gravesLabel = null!;
    private VBoxContainer _contextualActions = null!;
    private StatusBar _statusBar = null!;
    private TextureRect _selectionMarkerOverlay = null!;
    private PersonId? _selectedPersonId;
    private GraveId? _selectedGraveId;
    private double _tickAccumulator;

    // Faded in/out every frame in UpdateOcclusionFade depending on whether each one
    // currently sits between the camera and the selection.
    private readonly HashSet<Sprite3D> _fadedSprites = new();

    // A person walking to a resource node they were told to gather from, rather than one
    // already in range when the order was given. Resolved once they arrive (see
    // ResolvePendingGathers), so clicking a distant node reads as "go gather that" instead
    // of silently doing nothing the way a bare out-of-range GatherCommand would.
    private readonly Dictionary<PersonId, ResourceNodeId> _pendingGathers = new();

    public override void _Ready()
    {
        var contentRoot = ProjectSettings.GlobalizePath("res://Content");
        var configuration = new WorldConfiguration(
            ResourceCatalog.LoadFromDirectory(Path.Combine(contentRoot, "resources")),
            SkillCatalog.LoadFromDirectory(Path.Combine(contentRoot, "skills")),
            RecipeCatalog.LoadFromDirectory(Path.Combine(contentRoot, "recipes")),
            BuildingCatalog.LoadFromDirectory(Path.Combine(contentRoot, "buildings")),
            ItemCatalog.LoadFromDirectory(Path.Combine(contentRoot, "items")),
            SeasonParameters.Default);
        var map = MapLoader.LoadDefault(configuration);
        _world = map.World;
        _campCenter = map.CampCenter;

        GetViewport().PhysicsObjectPicking = true;

        SetUpLighting();
        SetUpTerrain();
        SetUpCamera();
        SetUpUi();

        _presenter = new WorldPresenter(this, _world, OnPersonClicked, OnResourceNodeSelected, OnGraveSelected, _terrain.SampleHeight);

        GD.Print($"Main ready. World has {_world.People.Count} people and {_world.ResourceNodes.Count} resource nodes at tick {_world.Clock.CurrentTick}.");
    }

    public override void _Process(double delta)
    {
        _cameraRig.HandleInput((float)delta);
        // Every rendered frame, not gated behind the tick accumulator below - both the
        // camera and the selected person's interpolated position move continuously between
        // ticks, so what's currently standing in the way of the view changes continuously too.
        UpdateOcclusionFade();
        UpdateSelectionMarkerOverlay();

        _tickAccumulator += delta;
        if (_tickAccumulator < TickIntervalSeconds)
        {
            return;
        }

        _tickAccumulator -= TickIntervalSeconds;
        if (_selectedPersonId is { } selectedPersonId)
        {
            _world.Execute(new GrantIdleGraceCommand(selectedPersonId, SelectedPersonIdleGraceTicks));
        }

        _world.Advance(1);
        ResolvePendingGathers();
        _statusBar.SetTick(_world.Clock.CurrentTick, _world.CurrentSeason);
        RefreshInfoLabel();
        RefreshBuildingsLabel();
        RefreshGravesLabel();

        foreach (var person in _world.People)
        {
            _presenter.SetPersonAlive(person.Id, person.IsAlive);
            // A person who dies mid-stride still has their view smoothly tween toward that
            // tick's (final) position over the next second, same as any other movement - one
            // last visible step before they stop forever, reading as the corpse still
            // "sliding" a little. Snapping instead (overSeconds: 0) once dead pins the view
            // to its exact final position immediately, with nothing left to glide.
            _presenter.SetPersonPosition(person.Id, person.Position, person.IsAlive ? (float)TickIntervalSeconds : 0f);
        }

        foreach (var node in _world.ResourceNodes)
        {
            if (!node.IsAlive)
            {
                // Catches nodes that withered from climate stress (see WorldState.Advance) -
                // felling already removes its own view immediately, this is just the passive
                // per-tick case.
                _presenter.RemoveResourceNodeView(node.Id);
                continue;
            }

            _presenter.SetResourceNodeHasFruit(node.Id, node.RemainingAmount > 0);
        }

        GD.Print($"Tick {_world.Clock.CurrentTick}: {_world.People.Count(p => p.IsAlive)} of {_world.People.Count} people alive.");
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.T })
        {
            _cameraRig.ToggleProjection();
        }
    }

    // _UnhandledInput, not _Input: _Input fires for every node before Godot's own UI system
    // gets a look at the event, so wheel/drag over a Control (e.g. scrolling the Inspector)
    // would zoom/rotate the camera underneath it too. That alone wasn't reliable (a
    // ScrollContainer with nothing left to scroll doesn't consume the wheel event, letting it
    // fall through), so this also explicitly bails out whenever the mouse is over any Control
    // at all - the camera should never react while the cursor is over UI, full stop.
    public override void _UnhandledInput(InputEvent @event)
    {
        if (GetViewport().GuiGetHoveredControl() is not null)
        {
            return;
        }

        _cameraRig.HandleMouseInput(@event);
    }

    // A decoration sprite (tree, rock, ...) between the camera and the selected person
    // otherwise just silently blocks the view of them with no way to tell where they went.
    private void UpdateOcclusionFade()
    {
        var occluding = ComputeOccludingSprites();

        foreach (var sprite in occluding)
        {
            if (_fadedSprites.Add(sprite))
            {
                SetSpriteAlpha(sprite, OcclusionFadedAlpha);
            }
        }

        _fadedSprites.RemoveWhere(sprite =>
        {
            if (occluding.Contains(sprite))
            {
                return false;
            }

            SetSpriteAlpha(sprite, 1f);
            return true;
        });
    }

    // Deliberately a blacklist, not a whitelist: earlier this only checked a hand-picked set
    // of sprite sources (decoration, then +resource nodes, then +buildings/graves/people...),
    // and every version was "missing" whatever wasn't added yet. Scanning every Sprite3D in
    // the scene instead means a new kind of entity is covered automatically, with no list to
    // remember to update - excluding only the two things that categorically don't belong:
    // ground shadow decals (not billboarded - see GroundShadow) and the selection's own
    // sprites (which sit at the target position itself, not in front of it).
    private HashSet<Sprite3D> ComputeOccludingSprites()
    {
        var result = new HashSet<Sprite3D>();
        if (_selectedPersonId is not { } personId || _presenter.GetPersonGlobalPosition(personId) is not { } targetPosition)
        {
            return result;
        }

        var cameraPosition = _cameraRig.CameraGlobalPosition;
        var toTarget = targetPosition - cameraPosition;
        var toTargetLength = toTarget.Length();
        if (toTargetLength <= 0.001f)
        {
            return result;
        }

        var direction = toTarget / toTargetLength;
        var selectedPersonNode = _presenter.GetPersonNode(personId);

        foreach (var child in FindChildren("*", nameof(Sprite3D), recursive: true, owned: false))
        {
            if (child is not Sprite3D sprite || sprite.Billboard == BaseMaterial3D.BillboardModeEnum.Disabled)
            {
                continue;
            }

            if (selectedPersonNode is not null && selectedPersonNode.IsAncestorOf(sprite))
            {
                continue;
            }

            var toSprite = sprite.GlobalPosition - cameraPosition;
            var along = toSprite.Dot(direction);
            // Beyond the target (along >= toTargetLength) or behind the camera (along <= 0)
            // isn't "in the way" of this particular line of sight - only strictly between
            // the two counts.
            if (along <= 0f || along >= toTargetLength)
            {
                continue;
            }

            var closestPointOnLine = cameraPosition + (direction * along);
            var perpendicularDistance = (sprite.GlobalPosition - closestPointOnLine).Length();
            // Every billboard here is square (all art is authored on a square canvas), so its
            // rendered world-space width equals PixelSize * pixel width - a wide tree needs a
            // much bigger "in the way" radius than a thin grass blade, not the same flat
            // distance regardless of how big it actually draws.
            var spriteRadius = (sprite.PixelSize * sprite.Texture.GetWidth()) / 2f;
            if (perpendicularDistance < spriteRadius + OcclusionMargin)
            {
                result.Add(sprite);
            }
        }

        return result;
    }

    private static void SetSpriteAlpha(Sprite3D sprite, float alpha)
    {
        var color = sprite.Modulate;
        color.A = alpha;
        sprite.Modulate = color;
    }

    // A 2D screen-space overlay, not a 3D billboard - see SelectionMarkerScreenSize's doc
    // comment for why. Camera3D.UnprojectPosition/IsPositionBehind do the actual perspective
    // math; this just anchors a plain Control on top of that one projected point.
    private void UpdateSelectionMarkerOverlay()
    {
        if (_selectedPersonId is not { } personId
            || _presenter.GetPersonGlobalPosition(personId) is not { } personPosition
            || _presenter.GetPersonHeadHeightOffset(personId) is not { } headHeightOffset)
        {
            _selectionMarkerOverlay.Visible = false;
            return;
        }

        var camera = _cameraRig.Camera;
        var headPosition = personPosition + new Vector3(0, headHeightOffset, 0);
        if (camera.IsPositionBehind(headPosition))
        {
            _selectionMarkerOverlay.Visible = false;
            return;
        }

        // SelectionMarkerScreenGap is screen pixels, not world meters - it belongs here,
        // applied to the projected point, not added to headPosition before projecting.
        var screenPosition = camera.UnprojectPosition(headPosition);
        _selectionMarkerOverlay.Position = new Vector2(
            screenPosition.X - (_selectionMarkerOverlay.Size.X / 2f),
            screenPosition.Y - SelectionMarkerScreenGap - _selectionMarkerOverlay.Size.Y);
        _selectionMarkerOverlay.Visible = true;
    }

    private void SetUpLighting()
    {
        AddChild(new DirectionalLight3D
        {
            Rotation = new Vector3(Mathf.DegToRad(-45), Mathf.DegToRad(-45), 0),
        });
    }

    private void SetUpTerrain()
    {
        _terrain = new TerrainRenderer(HeightmapPath, WaterwaysPath, GroundTexturePath);
        var groundBody = _terrain.BuildTerrainMesh(this);
        groundBody.InputEvent += OnGroundInputEvent;
        _terrain.BuildWaterways(this);

        var rng = new Random(DecorationScatterSeed);
        var campX = (float)_campCenter.X;
        var campZ = (float)_campCenter.Y;

        // texturePaths as params (last, not first, unlike the old single-texture signature)
        // so a call can list one texture or several equally-likely variants - see
        // TerrainRenderer.ScatterDecoration's own doc comment.
        void Scatter(int count, float height, Color fallbackColor, float centerX, float centerZ, float radius, params string[] texturePaths)
            => _terrain.ScatterDecoration(this, rng, count, texturePaths, height, fallbackColor, DecorationMinScale, DecorationMaxScale, centerX, centerZ, radius);

        // A single disk of the full radius draws as one perfect, hard-edged circle once
        // you're zoomed out enough to see its whole outline - unmistakably a "stamp", not a
        // patch of forest. Splitting it into several smaller, jittered, differently-sized
        // disks and letting their footprints overlap is the same fix generate_sprites.py's
        // lobe_cluster_mask already uses for canopy silhouettes (many small unioned blobs,
        // not one primitive) - the union's outline is irregular/scalloped instead.
        //
        // Ground cover (grass/flower/fern) deliberately isn't part of this - a real meadow
        // is a continuous carpet, not clumps with bare gaps between them the way trees
        // actually do grow, so those are scattered once over the *whole* disk instead (see
        // the plain Scatter calls right after each ScatterClump call below).
        void ScatterClump(
            float centerX, float centerZ, float radius, int subClusters,
            int treeCount, int deciduousCount, int bushCount, int rockCount, int stumpCount, int fallenLogCount)
        {
            for (var sub = 0; sub < subClusters; sub++)
            {
                var subAngle = (float)rng.NextDouble() * Mathf.Tau;
                var subCenterOffset = radius * 0.35f * (float)rng.NextDouble();
                var subX = centerX + (MathF.Cos(subAngle) * subCenterOffset);
                var subZ = centerZ + (MathF.Sin(subAngle) * subCenterOffset);
                var subRadius = radius * (0.55f + ((float)rng.NextDouble() * 0.35f));

                Scatter(treeCount / subClusters, TreeHeightMeters, TreeFallbackColor, subX, subZ, subRadius, ConiferTreePath);
                Scatter(deciduousCount / subClusters, DeciduousTreeHeightMeters, DeciduousTreeFallbackColor, subX, subZ, subRadius, DeciduousTreePath);
                Scatter(bushCount / subClusters, BushHeightMeters, BushFallbackColor, subX, subZ, subRadius, BushPath);
                Scatter(rockCount / subClusters, RockHeightMeters, RockFallbackColor, subX, subZ, subRadius, RockPilePath, RockBoulderPath, RockClusterPath);
                Scatter(stumpCount / subClusters, StumpHeightMeters, StumpFallbackColor, subX, subZ, subRadius, TreeStumpPath);
                Scatter(fallenLogCount / subClusters, FallenLogHeightMeters, FallenLogFallbackColor, subX, subZ, subRadius, FallenLogPath);
            }
        }

        const int denseZoneSubClusters = 5;
        ScatterClump(campX, campZ, DecorationRadius, denseZoneSubClusters, TreeCount, DeciduousTreeCount, BushCount, RockCount, StumpCount, FallenLogCount);
        Scatter(GrassCount, GrassHeightMeters, GrassFallbackColor, campX, campZ, DecorationRadius, GrassPath);
        Scatter(FlowerCount, FlowerHeightMeters, FlowerFallbackColor, campX, campZ, DecorationRadius, FlowerPath);
        Scatter(FernCount, FernHeightMeters, FernFallbackColor, campX, campZ, DecorationRadius, FernPath);

        const int groveSubClusters = 3;
        for (var i = 0; i < GroveCount; i++)
        {
            var groveX = ((float)rng.NextDouble() - 0.5f) * 2f * _terrain.Half;
            var groveZ = ((float)rng.NextDouble() - 0.5f) * 2f * _terrain.Half;
            ScatterClump(
                groveX, groveZ, GroveRadius, groveSubClusters,
                GroveTreeCount, GroveDeciduousTreeCount, GroveBushCount, GroveRockCount, GroveStumpCount, GroveFallenLogCount);
            Scatter(GroveGrassCount, GrassHeightMeters, GrassFallbackColor, groveX, groveZ, GroveRadius, GrassPath);
            Scatter(GroveFlowerCount, FlowerHeightMeters, FlowerFallbackColor, groveX, groveZ, GroveRadius, FlowerPath);
            Scatter(GroveFernCount, FernHeightMeters, FernFallbackColor, groveX, groveZ, GroveRadius, FernPath);
        }

        Scatter(WideTreeCount, TreeHeightMeters, TreeFallbackColor, 0f, 0f, _terrain.Half, ConiferTreePath);
        Scatter(WideDeciduousTreeCount, DeciduousTreeHeightMeters, DeciduousTreeFallbackColor, 0f, 0f, _terrain.Half, DeciduousTreePath);
        Scatter(WideBushCount, BushHeightMeters, BushFallbackColor, 0f, 0f, _terrain.Half, BushPath);
        Scatter(WideGrassCount, GrassHeightMeters, GrassFallbackColor, 0f, 0f, _terrain.Half, GrassPath);
        Scatter(WideFlowerCount, FlowerHeightMeters, FlowerFallbackColor, 0f, 0f, _terrain.Half, FlowerPath);
        Scatter(WideFernCount, FernHeightMeters, FernFallbackColor, 0f, 0f, _terrain.Half, FernPath);
        Scatter(WideRockCount, RockHeightMeters, RockFallbackColor, 0f, 0f, _terrain.Half, RockPilePath, RockBoulderPath, RockClusterPath);
        Scatter(WideStumpCount, StumpHeightMeters, StumpFallbackColor, 0f, 0f, _terrain.Half, TreeStumpPath);
        Scatter(WideFallenLogCount, FallenLogHeightMeters, FallenLogFallbackColor, 0f, 0f, _terrain.Half, FallenLogPath);
    }

    private void SetUpCamera()
    {
        var campX = (float)_campCenter.X;
        var campZ = (float)_campCenter.Y;
        var campPosition = new Vector3(campX, _terrain.SampleHeight(campX, campZ), campZ);
        _cameraRig = new FreeCameraRig(this, campPosition, InitialZoomDistance, MinZoom, MaxZoom);
    }

    private void SetUpUi()
    {
        var canvas = new CanvasLayer();
        AddChild(canvas);

        SetUpInspectorWindow(canvas);
        SetUpStatusBar(canvas);
        SetUpSelectionMarker(canvas);
    }

    private void SetUpSelectionMarker(CanvasLayer canvas)
    {
        _selectionMarkerOverlay = new TextureRect
        {
            Texture = ResourceLoader.Load<Texture2D>(SelectionMarkerTexturePath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspect,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Size = new Vector2(SelectionMarkerScreenSize, SelectionMarkerScreenSize),
            Visible = false,
        };
        canvas.AddChild(_selectionMarkerOverlay);
    }

    private static StyleBoxFlat PanelBackground() => new()
    {
        BgColor = new Color(0f, 0f, 0f, 0.6f),
        ContentMarginLeft = 12,
        ContentMarginRight = 12,
        ContentMarginTop = 10,
        ContentMarginBottom = 10,
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
    };

    // One floating, collapsible window for both the inspector and the action buttons -
    // the buttons are contextual to whichever person is selected, so they belong together
    // rather than in a separate always-open panel.
    private void SetUpInspectorWindow(CanvasLayer canvas)
    {
        const float width = 340f;

        var panel = new FloatingPanel("Inspector")
        {
            Position = new Vector2(16, 16),
            CustomMinimumSize = new Vector2(width, 0),
        };
        panel.AddThemeStyleboxOverride("panel", PanelBackground());
        canvas.AddChild(panel);

        _infoLabel = new Label
        {
            Text = "No selection.",
            CustomMinimumSize = new Vector2(width, 0),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        panel.Body.AddChild(_infoLabel);

        var spawnButton = new Button { Text = "Spawn Person" };
        spawnButton.Pressed += OnSpawnButtonPressed;
        panel.Body.AddChild(spawnButton);

        _contextualActions = new VBoxContainer { Visible = false };
        panel.Body.AddChild(_contextualActions);

        var craftButton = new Button { Text = "Craft Axe (5 Wood)" };
        craftButton.Pressed += OnCraftButtonPressed;
        _contextualActions.AddChild(craftButton);

        var craftClothingButton = new Button { Text = "Craft Warm Clothing (10 Wood)" };
        craftClothingButton.Pressed += OnCraftClothingButtonPressed;
        _contextualActions.AddChild(craftClothingButton);

        var craftBasketButton = new Button { Text = "Craft Basket (8 Wood)" };
        craftBasketButton.Pressed += OnCraftBasketButtonPressed;
        _contextualActions.AddChild(craftBasketButton);

        var craftBagButton = new Button { Text = "Craft Bag (10 Grass)" };
        craftBagButton.Pressed += OnCraftBagButtonPressed;
        _contextualActions.AddChild(craftBagButton);

        var buildButton = new Button { Text = "Build Storage Hut (20 Wood)" };
        buildButton.Pressed += OnBuildButtonPressed;
        _contextualActions.AddChild(buildButton);

        var repairButton = new Button { Text = "Repair Nearest Building (5 Wood)" };
        repairButton.Pressed += OnRepairButtonPressed;
        _contextualActions.AddChild(repairButton);

        var depositButton = new Button { Text = "Deposit Wood -> Nearest Building" };
        depositButton.Pressed += OnDepositButtonPressed;
        _contextualActions.AddChild(depositButton);

        var withdrawButton = new Button { Text = "Withdraw Wood <- Nearest Building" };
        withdrawButton.Pressed += OnWithdrawButtonPressed;
        _contextualActions.AddChild(withdrawButton);

        var fellButton = new Button { Text = "Fell Nearest Tree" };
        fellButton.Pressed += OnFellButtonPressed;
        _contextualActions.AddChild(fellButton);

        var buryButton = new Button { Text = "Bury Nearest Dead Person" };
        buryButton.Pressed += OnBuryButtonPressed;
        _contextualActions.AddChild(buryButton);

        var lootButton = new Button { Text = "Loot Nearest Dead Person" };
        lootButton.Pressed += OnLootButtonPressed;
        _contextualActions.AddChild(lootButton);

        var eatButton = new Button { Text = "Eat" };
        eatButton.Pressed += OnEatButtonPressed;
        _contextualActions.AddChild(eatButton);

        _buildingsLabel = new Label { Text = "Buildings: none" };
        panel.Body.AddChild(_buildingsLabel);

        _gravesLabel = new Label { Text = "Graves: none" };
        panel.Body.AddChild(_gravesLabel);
    }

    private void SetUpStatusBar(CanvasLayer canvas)
    {
        _statusBar = new StatusBar();
        _statusBar.AddThemeStyleboxOverride("panel", PanelBackground());
        canvas.AddChild(_statusBar);
        _statusBar.SetTick(0, _world.CurrentSeason);
    }

    private void OnSpawnButtonPressed()
    {
        _world.Execute(new SpawnPersonCommand($"Person {_world.People.Count + 1}", FindFreeSpawnPosition()));
    }

    private void OnCraftButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then craft.");
            return;
        }

        _world.Execute(new CraftCommand(personId, new ItemKindId("axe")));
        RefreshInfoLabel();
    }

    private void OnCraftClothingButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then craft.");
            return;
        }

        _world.Execute(new CraftCommand(personId, new ItemKindId("warm_clothing")));
        RefreshInfoLabel();
    }

    private void OnCraftBasketButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then craft.");
            return;
        }

        _world.Execute(new CraftCommand(personId, new ItemKindId("basket")));
        RefreshInfoLabel();
    }

    private void OnCraftBagButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then craft.");
            return;
        }

        _world.Execute(new CraftCommand(personId, new ItemKindId("bag")));
        RefreshInfoLabel();
    }

    private void OnBuildButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then build.");
            return;
        }

        var person = _world.People.FirstOrDefault(p => p.Id == personId);
        if (person is null)
        {
            return;
        }

        var buildPosition = FindFreeBuildingPosition(person.Position);
        _world.Execute(new ConstructCommand(personId, new BuildingKindId("storage_hut"), buildPosition));
        RefreshInfoLabel();
        RefreshBuildingsLabel();
    }

    private void OnRepairButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then repair.");
            return;
        }

        var person = _world.People.FirstOrDefault(p => p.Id == personId);
        if (person is null)
        {
            return;
        }

        var nearestBuilding = FindNearestBuilding(person.Position);
        if (nearestBuilding is null)
        {
            _statusBar.Notify("No buildings to repair yet.");
            return;
        }

        if (WorldState.Distance(person.Position, nearestBuilding.Position) > WorldState.MaxInteractionDistance)
        {
            _statusBar.Notify("The nearest building is too far away.");
            return;
        }

        _world.Execute(new RepairCommand(personId, nearestBuilding.Id));
        RefreshInfoLabel();
        RefreshBuildingsLabel();
    }

    private void OnDepositButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then deposit.");
            return;
        }

        var person = _world.People.FirstOrDefault(p => p.Id == personId);
        if (person is null)
        {
            return;
        }

        var nearestBuilding = FindNearestBuilding(person.Position);
        if (nearestBuilding is null)
        {
            _statusBar.Notify("No buildings to deposit into yet.");
            return;
        }

        if (WorldState.Distance(person.Position, nearestBuilding.Position) > WorldState.MaxInteractionDistance)
        {
            _statusBar.Notify("The nearest building is too far away.");
            return;
        }

        var woodItem = new ItemKindId("wood");
        var amount = person.Inventory.Get(woodItem);
        if (amount <= 0)
        {
            _statusBar.Notify("No wood to deposit.");
            return;
        }

        _world.Execute(new DepositCommand(personId, nearestBuilding.Id, woodItem, amount));
        RefreshInfoLabel();
        RefreshBuildingsLabel();
    }

    private void OnWithdrawButtonPressed()
    {
        const int withdrawAmount = 20;

        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then withdraw.");
            return;
        }

        var person = _world.People.FirstOrDefault(p => p.Id == personId);
        if (person is null)
        {
            return;
        }

        var nearestBuilding = FindNearestBuilding(person.Position);
        if (nearestBuilding is null)
        {
            _statusBar.Notify("No buildings to withdraw from yet.");
            return;
        }

        if (WorldState.Distance(person.Position, nearestBuilding.Position) > WorldState.MaxInteractionDistance)
        {
            _statusBar.Notify("The nearest building is too far away.");
            return;
        }

        var woodItem = new ItemKindId("wood");
        var amount = Math.Min(withdrawAmount, nearestBuilding.Inventory.Get(woodItem));
        if (amount <= 0)
        {
            _statusBar.Notify("No wood to withdraw.");
            return;
        }

        _world.Execute(new WithdrawCommand(personId, nearestBuilding.Id, woodItem, amount));
        RefreshInfoLabel();
        RefreshBuildingsLabel();
    }

    private void OnFellButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then fell.");
            return;
        }

        var person = _world.People.FirstOrDefault(p => p.Id == personId);
        if (person is null)
        {
            return;
        }

        var node = FindNearestFellableResourceNode(person.Position);
        if (node is null)
        {
            _statusBar.Notify("No trees nearby to fell.");
            return;
        }

        if (WorldState.Distance(person.Position, node.Position) > WorldState.MaxInteractionDistance)
        {
            _statusBar.Notify("The nearest tree is too far away.");
            return;
        }

        _world.Execute(new FellCommand(personId, node.Id));
        _presenter.RemoveResourceNodeView(node.Id);
        RefreshInfoLabel();
    }

    private void OnBuryButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then bury.");
            return;
        }

        var person = _world.People.FirstOrDefault(p => p.Id == personId);
        if (person is null)
        {
            return;
        }

        // Without this, a selected person who is themselves dead-and-unburied reads as their
        // own nearest deceased (distance 0) - BuryCommand silently no-ops (it requires the
        // burying person to be alive), but the corpse's view still got removed below as if it
        // had actually been buried, vanishing with no grave ever created.
        if (!person.IsAlive)
        {
            _statusBar.Notify("A dead person can't bury anyone.");
            return;
        }

        var deceased = FindNearestUnburiedDeceased(person.Position);
        if (deceased is null)
        {
            _statusBar.Notify("No one left to bury.");
            return;
        }

        if (WorldState.Distance(person.Position, deceased.Position) > WorldState.MaxInteractionDistance)
        {
            _statusBar.Notify("The nearest deceased person is too far away.");
            return;
        }

        _world.Execute(new BuryCommand(personId, deceased.Id));
        _presenter.RemovePersonView(deceased.Id);
        RefreshInfoLabel();
        RefreshGravesLabel();
    }

    private void OnLootButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then loot.");
            return;
        }

        var person = _world.People.FirstOrDefault(p => p.Id == personId);
        if (person is null)
        {
            return;
        }

        // Same reasoning as OnBuryButtonPressed: a dead selected person could otherwise loot
        // their own corpse.
        if (!person.IsAlive)
        {
            _statusBar.Notify("A dead person can't loot anyone.");
            return;
        }

        var deceased = FindNearestLootableDeceased(person.Position);
        if (deceased is null)
        {
            _statusBar.Notify("Nothing left to loot.");
            return;
        }

        if (WorldState.Distance(person.Position, deceased.Position) > WorldState.MaxInteractionDistance)
        {
            _statusBar.Notify("The nearest belongings are too far away.");
            return;
        }

        _world.Execute(new LootCommand(personId, deceased.Id));
        RefreshInfoLabel();
    }

    // Eats from whatever food kinds are on hand - gathering food only fills the inventory now
    // (see GatherCommand), so a person never gets fed without this. Tries every kind currently
    // carried rather than requiring the player to pick one; EatCommand itself no-ops for any
    // kind that isn't food, so this is safe to call across the whole inventory.
    private void OnEatButtonPressed()
    {
        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then eat.");
            return;
        }

        var person = _world.People.FirstOrDefault(p => p.Id == personId);
        if (person is null)
        {
            return;
        }

        foreach (var item in person.Inventory.Counts.Keys.ToList())
        {
            if (person.Needs.Hunger <= 0f)
            {
                break;
            }

            _world.Execute(new EatCommand(personId, item));
        }

        RefreshInfoLabel();
    }

    private Building? FindNearestBuilding(Position position) =>
        _world.Buildings.OrderBy(b => WorldState.Distance(b.Position, position)).FirstOrDefault();

    private ResourceNode? FindNearestFellableResourceNode(Position position) =>
        _world.ResourceNodes
            .Where(n => n.IsAlive && _world.ResourceCatalog.Get(n.Kind).CanFell)
            .OrderBy(n => WorldState.Distance(n.Position, position))
            .FirstOrDefault();

    private Person? FindNearestUnburiedDeceased(Position position) =>
        _world.People
            .Where(p => !p.IsAlive && !p.IsBuried)
            .OrderBy(p => WorldState.Distance(p.Position, position))
            .FirstOrDefault();

    private Person? FindNearestLootableDeceased(Position position) =>
        _world.People
            .Where(p => !p.IsAlive && p.Inventory.Counts.Count > 0)
            .OrderBy(p => WorldState.Distance(p.Position, position))
            .FirstOrDefault();

    private Position FindFreeSpawnPosition()
    {
        const float minDistance = 1.2f;
        const float spread = 16f;
        const int maxAttempts = 20;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var candidate = new Position(
                _campCenter.X + ((GD.Randf() - 0.5f) * spread),
                _campCenter.Y + ((GD.Randf() - 0.5f) * spread));
            var tooClose = _world.People.Any(p => WorldState.Distance(p.Position, candidate) < minDistance);
            if (!tooClose)
            {
                return candidate;
            }
        }

        return new Position(
            _campCenter.X + ((GD.Randf() - 0.5f) * spread),
            _campCenter.Y + ((GD.Randf() - 0.5f) * spread));
    }

    private Position FindFreeBuildingPosition(Position near)
    {
        const float minDistance = 1.5f;
        // Kept within MaxInteractionDistance's worst-case diagonal (spread/2 * sqrt(2)) so a freshly
        // picked spot is never too far away to actually construct on, since ConstructCommand itself
        // now requires proximity.
        const float spread = WorldState.MaxInteractionDistance;
        const int maxAttempts = 20;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var candidate = new Position(
                near.X + ((GD.Randf() - 0.5f) * spread),
                near.Y + ((GD.Randf() - 0.5f) * spread));
            var blocked = _world.Buildings.Any(b => WorldState.Distance(b.Position, candidate) < minDistance)
                || _world.People.Any(p => WorldState.Distance(p.Position, candidate) < minDistance);
            if (!blocked)
            {
                return candidate;
            }
        }

        return new Position(near.X + ((GD.Randf() - 0.5f) * spread), near.Y + ((GD.Randf() - 0.5f) * spread));
    }

    private void OnPersonClicked(PersonId id, MouseButton button)
    {
        if (button == MouseButton.Right)
        {
            TeachFromSelectedPersonTo(id);
            return;
        }

        _selectedPersonId = id;
        _selectedGraveId = null;
        _contextualActions.Visible = true;
        RefreshInfoLabel();
    }

    private void OnGraveSelected(GraveId id)
    {
        _selectedGraveId = id;
        _selectedPersonId = null;
        _contextualActions.Visible = false;
        RefreshInfoLabel();
    }

    private void TeachFromSelectedPersonTo(PersonId studentId)
    {
        if (_selectedPersonId is not { } teacherId || teacherId == studentId)
        {
            return;
        }

        var teacher = _world.People.FirstOrDefault(p => p.Id == teacherId);
        if (teacher is null)
        {
            return;
        }

        foreach (var technique in teacher.KnownTechniques)
        {
            _world.Execute(new TeachCommand(teacherId, studentId, technique));
        }

        RefreshInfoLabel();
    }

    private void OnResourceNodeSelected(ResourceNodeId id)
    {
        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then click a resource node to gather.");
            return;
        }

        var person = _world.People.FirstOrDefault(p => p.Id == personId);
        var node = _world.ResourceNodes.FirstOrDefault(n => n.Id == id);
        if (person is null || node is null)
        {
            return;
        }

        if (WorldState.Distance(person.Position, node.Position) > WorldState.MaxInteractionDistance)
        {
            _pendingGathers[personId] = id;
            _world.Execute(new MoveCommand(personId, ApproachPosition(person.Position, node.Position, ApproachDistance)));
            RefreshInfoLabel();
            return;
        }

        _pendingGathers.Remove(personId);
        GatherFrom(personId, node);
        RefreshInfoLabel();
    }

    // Runs once a person who was walking to a resource node (see OnResourceNodeSelected)
    // arrives, so clicking a distant node reads as "go gather that" rather than the person
    // just standing there once they arrive.
    private void ResolvePendingGathers()
    {
        if (_pendingGathers.Count == 0)
        {
            return;
        }

        foreach (var (personId, nodeId) in _pendingGathers.ToList())
        {
            var person = _world.People.FirstOrDefault(p => p.Id == personId && p.IsAlive);
            var node = _world.ResourceNodes.FirstOrDefault(n => n.Id == nodeId);
            if (person is null || node is null)
            {
                _pendingGathers.Remove(personId);
                continue;
            }

            if (WorldState.Distance(person.Position, node.Position) > WorldState.MaxInteractionDistance)
            {
                continue;
            }

            _pendingGathers.Remove(personId);
            GatherFrom(personId, node);
        }
    }

    // Depleting a node down to zero doesn't remove its view - the plant/tree is still there,
    // just fruitless until RegenPerTick brings it back. Only IsAlive turning false (felled or
    // withered - see FellCommand, WorldState.Advance) means the thing itself is actually gone.
    private void GatherFrom(PersonId personId, ResourceNode node) => _world.Execute(new GatherCommand(personId, node.Id));

    // A destination short of the node's own position, approaching from wherever the
    // person currently is - so they end up standing next to the resource rather than
    // walking on top of and visually covering it.
    private static Position ApproachPosition(Position from, Position to, float standoffDistance)
    {
        var dx = from.X - to.X;
        var dy = from.Y - to.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        if (distance <= standoffDistance)
        {
            return from;
        }

        var ratio = standoffDistance / distance;
        return new Position(to.X + (dx * ratio), to.Y + (dy * ratio));
    }

    private void OnGroundInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            return;
        }

        if (_selectedPersonId is not { } personId)
        {
            _statusBar.Notify("Select a person first, then click the ground to walk there.");
            return;
        }

        _world.Execute(new MoveCommand(personId, new Position(position.X, position.Z)));
        RefreshInfoLabel();
    }

    private void RefreshInfoLabel()
    {
        if (_selectedGraveId is { } graveId)
        {
            var grave = _world.Graves.FirstOrDefault(g => g.Id == graveId);
            _infoLabel.Text = grave is null ? "No selection." : GraveText(grave);
            return;
        }

        var person = _selectedPersonId is { } id ? _world.People.FirstOrDefault(p => p.Id == id) : null;
        if (person is null)
        {
            _infoLabel.Text = "No selection.";
            return;
        }

        var status = person.IsAlive ? string.Empty : " [dead]";
        var skills = person.Skills.Levels.Count > 0
            ? string.Join(", ", person.Skills.Levels.Select(kv => $"{kv.Key}: {kv.Value}"))
            : "none";
        var techniques = person.KnownTechniques.Count > 0
            ? string.Join(", ", person.KnownTechniques)
            : "none";
        var inventory = person.Inventory.Counts.Count > 0
            ? string.Join(", ", person.Inventory.Counts.Select(kv => $"{kv.Key} x{kv.Value}"))
            : "empty";
        var carriedWeight = person.Inventory.TotalWeight(_world.ItemCatalog);
        var maxCarryWeight = _world.MaxCarryWeightFor(person);
        _infoLabel.Text =
            $"{person.Id}  {person.Name}{status}\n" +
            $"Position: {person.Position}\n" +
            $"Age: {AgeText(person)}\n" +
            $"Task: {TaskText(person)}\n" +
            $"Hunger: {person.Needs.Hunger}  Fatigue: {person.Needs.Fatigue}\n" +
            $"Skills: {skills}\n" +
            $"Known techniques: {techniques}\n" +
            $"Carrying: {carriedWeight}/{maxCarryWeight}\n" +
            $"Inventory: {inventory}";
    }

    private static string TaskText(Person person) => person.Tasks.Current switch
    {
        MoveTask move => $"Walking to {move.Destination}",
        _ => "Idle",
    };

    private static string GraveText(Grave grave)
    {
        if (!grave.IsMarked)
        {
            return $"{grave.Id}\nPosition: {grave.Position}\nUnmarked grave - no record survives.";
        }

        var techniques = grave.KnownTechniques.Count > 0
            ? string.Join(", ", grave.KnownTechniques)
            : "none";
        var causeText = grave.CauseOfDeath switch
        {
            DeathCause.Hunger => " of hunger",
            DeathCause.OldAge => " of old age",
            _ => string.Empty,
        };
        return
            $"{grave.Id}\n" +
            $"Position: {grave.Position}\n" +
            $"{grave.Name}, died at age {grave.AgeAtDeath} winter{(grave.AgeAtDeath == 1 ? "" : "s")}{causeText}\n" +
            $"{ParentsText(grave.MotherName, grave.FatherName)}" +
            $"Known techniques: {techniques}";
    }

    private static string ParentsText(string? motherName, string? fatherName)
    {
        if (motherName is null && fatherName is null)
        {
            return string.Empty;
        }

        var parents = string.Join(" and ", new[] { motherName, fatherName }.Where(name => name is not null));
        return $"Child of {parents}\n";
    }

    private void RefreshBuildingsLabel()
    {
        _buildingsLabel.Text = "Buildings: " + (_world.Buildings.Count > 0
            ? string.Join(", ", _world.Buildings.Select(BuildingSummary))
            : "none");
    }

    private string AgeText(Person person)
    {
        var winters = _world.AgeInYears(person);
        if (winters >= 1)
        {
            return $"{winters} winter{(winters == 1 ? "" : "s")}";
        }

        var seasons = _world.AgeInSeasons(person);
        return $"{seasons} season{(seasons == 1 ? "" : "s")}";
    }

    private void RefreshGravesLabel()
    {
        _gravesLabel.Text = $"Graves: {_world.Graves.Count}";
    }

    private static string BuildingSummary(Building building)
    {
        var inventory = building.Inventory.Counts.Count > 0
            ? string.Join(", ", building.Inventory.Counts.Select(kv => $"{kv.Key} x{kv.Value}"))
            : "empty";
        return $"{building.Kind} #{building.Id} ({building.Condition:0}%) [{inventory}]";
    }
}
