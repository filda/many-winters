using Godot;
using ManyWinters.Core.Commands;
using ManyWinters.Core.Construction;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Maps;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;
using ManyWinters.Godot.Views;
using ManyWinters.Godot.Fog;
using ManyWinters.Godot.Terrain;
using ManyWinters.Godot.Interaction;
using ManyWinters.Godot.Ui;
using ManyWinters.Godot.Sprites;

namespace ManyWinters.Godot;

public partial class Main : Node3D
{
    private const string SelectionMarkerTexturePath = "res://Content/people/selection_marker.png";

    // Every tunable number this scene runs on lives in these two, not in constants here - what
    // is a rule of the world itself belongs in SimulationRules (via WorldConfiguration) instead.
    private readonly SimulationPacing _pacing = SimulationPacing.Default;
    private readonly PresentationSettings _presentation = PresentationSettings.Default;

    private WorldState _world = null!;
    private RevealableExploration _exploration = null!;
    private WorldPresenter _presenter = null!;
    private FogOfWarRenderer _fogOfWar = null!;
    private GroundClouds _groundClouds = null!;
    private CloudFogMask _cloudFogMask = null!;
    private TerrainRenderer _terrain = null!;
    private FreeCameraRig _cameraRig = null!;
    private Position _campCenter;

    private Label _infoLabel = null!;
    private Label _buildingsLabel = null!;
    private Label _gravesLabel = null!;
    private VBoxContainer _contextualActions = null!;
    private StatusBar _statusBar = null!;
    private TextureRect _selectionMarkerOverlay = null!;
    // The selection is the Person itself, not an id - every command and every label wants the
    // object, and the views hand it over on click (see PersonView), so there's never a lookup
    // between "clicked" and "acted on".
    private Person? _selectedPerson;
    private Grave? _selectedGrave;
    private double _tickAccumulator;

    // Faded in/out every frame in UpdateOcclusionFade depending on whether each one
    // currently sits between the camera and the selection.

    // A person walking to a resource node they were told to gather from, rather than one
    // already in range when the order was given. Resolved once they arrive (see
    // ResolvePendingGathers), so clicking a distant node reads as "go gather that" instead
    // of silently doing nothing the way a bare out-of-range GatherCommand would.
    private readonly Dictionary<Person, ResourceNode> _pendingGathers = new();

    public override void _Ready()
    {
        var configuration = WorldConfiguration.LoadFromJson(catalog => ContentFiles.ReadJsonTree($"res://Content/{catalog}"));
        var map = MapLoader.LoadDefault(configuration);
        _world = map.World;
        _exploration = new RevealableExploration(_world.Exploration);
        _campCenter = map.CampCenter;

        GetViewport().PhysicsObjectPicking = true;

        SetUpLighting();
        SetUpTerrain();
        SetUpCamera();
        SetUpUi();

        CloudScatter.Scatter(this, _terrain.Half);
        _cloudFogMask = new CloudFogMask(this, _cameraRig.Camera);

        _presenter = new WorldPresenter(this, _world, _exploration, OnPersonClicked, OnResourceNodeSelected, OnGraveSelected, OnMissedClick, _terrain.SampleHeight);
        _fogOfWar = new FogOfWarRenderer(_exploration, _terrain.Half, _cameraRig.Camera, _cloudFogMask);
        _groundClouds = new GroundClouds(this, _fogOfWar, _terrain.Half, _terrain.SampleHeight);

        GD.Print($"Main ready. World has {_world.People.Count} people and {_world.ResourceNodes.Count} resource nodes at tick {_world.Clock.CurrentTick}.");
        // A permanent build tag, answering "am I actually running the build I think I'm
        // running" (a repeated real source of confusion - the editor's own hot-reload, or
        // forgetting to relaunch, can silently leave an old process running) with a one-line
        // log check. Derived from the assembly this code is executing out of rather than
        // hand-written: a string that has to be bumped by hand is only ever as truthful as
        // the last person who remembered to bump it, and this one had gone stale by dozens
        // of builds.
        GD.Print($"Build tag: {BuildTag.For(AssemblyBuildTimeUtc())}");
    }

    // When the running assembly was last written - the closest thing to a build stamp that
    // needs no build-time code generation, and one that cannot drift out of date the way the
    // hand-written tag it replaced did. Built from the directory rather than
    // Assembly.Location, which is empty here: Godot loads the project assembly from a stream
    // so the file can be overwritten while the editor still holds it. Null rather than a
    // guess if there is no such file to stat, which BuildTag renders as an explicit
    // "unknown" instead of a plausible-looking lie.
    private static DateTimeOffset? AssemblyBuildTimeUtc()
    {
        var assemblyPath = Path.Combine(AppContext.BaseDirectory, $"{typeof(Main).Assembly.GetName().Name}.dll");

        return File.Exists(assemblyPath)
            ? new DateTimeOffset(File.GetLastWriteTimeUtc(assemblyPath), TimeSpan.Zero)
            : null;
    }

    public override void _Process(double delta)
    {
        _cameraRig.HandleInput((float)delta);
        // Every rendered frame, not gated behind the tick accumulator below - both the
        // camera and the selected person's interpolated position move continuously between
        // ticks, so what's currently standing in the way of the view changes continuously too.
        UpdateOcclusionFade();
        UpdateSelectionMarkerOverlay();
        // Also every frame: taking hover is driven by mouse movement, but losing it isn't -
        // a person can simply walk out from under a cursor that never moved (see HoverArbiter).
        _presenter.RevalidateHover();
        // Also every frame, same reasoning - the mask camera has to track the main
        // camera's own continuous movement/zoom, not just once per simulation tick.
        _cloudFogMask.Update();

        _tickAccumulator += delta;
        if (_tickAccumulator < _pacing.TickIntervalSeconds)
        {
            return;
        }

        _tickAccumulator -= _pacing.TickIntervalSeconds;
        if (_selectedPerson is { } selectedPerson)
        {
            _world.Execute(new GrantIdleGraceCommand(selectedPerson, _pacing.SelectedPersonIdleGraceTicks));
        }

        _world.Advance(1);
        _presenter.RefreshExploration();
        _fogOfWar.Refresh();
        _groundClouds.Refresh();
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
            _presenter.SetPersonPosition(person.Id, person.Position, person.IsAlive ? (float)_pacing.TickIntervalSeconds : 0f);
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

        // Checked ahead of Godot's own physics-object-picking (which fires later in the same
        // input dispatch, from unhandled input) precisely so it can win even when that pick
        // would have legitimately landed on something opaque standing in front of a person -
        // see PresentationSettings.PersonClickScreenRadius's own doc comment.
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouseButton
            && GetViewport().GuiGetHoveredControl() is null
            && FindNearestPersonOnScreen(mouseButton.Position) is { } person)
        {
            OnPersonClicked(person, MouseButton.Left);
            GetViewport().SetInputAsHandled();
        }
    }

    // The already-selected person is deliberately never a candidate: re-selecting them is a
    // no-op, and the radius around them used to swallow every click on the ground right at
    // their feet (the very place a player aims a short "step over there" order), so the
    // person just stood still. With them excluded the click falls through to picking as
    // usual - their own opaque pixels still re-select them harmlessly, anything else is the
    // ground or a real neighbour.
    private Person? FindNearestPersonOnScreen(Vector2 screenPosition)
    {
        var camera = _cameraRig.Camera;
        Person? nearest = null;
        var nearestDistance = float.MaxValue;

        foreach (var person in _world.People)
        {
            if (person == _selectedPerson
                || _presenter.GetPersonGlobalPosition(person.Id) is not { } personGlobalPosition
                || camera.IsPositionBehind(personGlobalPosition))
            {
                continue;
            }

            var distance = camera.UnprojectPosition(personGlobalPosition).DistanceTo(screenPosition);
            if (distance <= _presentation.PersonClickScreenRadius && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = person;
            }
        }

        return nearest;
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

        // Re-applied every frame, not just on first entering the set - ResourceNodeView's
        // hover highlight writes this exact same sprite's Modulate independently (on every
        // mouse-move hover-state change, with no idea occlusion fade exists) and would
        // otherwise silently undo the fade the moment the cursor happens to sit on top of
        // whatever's currently occluding - easy to hit for a big nearby canopy that already
        // fills much of the screen (docs/Screenshot 2026-09-01 223350.png). Cheap either
        // way - the occluding set is a handful of sprites, never the whole scene.
        // The faded set lives in BillboardSprite, not here, because picking has to consult
        // it too (see BillboardSprite.OcclusionFadedSprites' doc comment) - this is still
        // the only place that decides what goes in and out of it.
        foreach (var sprite in occluding)
        {
            BillboardSprite.SetOcclusionFaded(sprite, true);
            SetSpriteAlpha(sprite, _presentation.OcclusionFadedAlpha);
        }

        // Materialized first: un-fading mutates the very set being walked.
        var noLongerOccluding = BillboardSprite.OcclusionFadedSprites.Where(sprite => !occluding.Contains(sprite)).ToList();
        foreach (var sprite in noLongerOccluding)
        {
            BillboardSprite.SetOcclusionFaded(sprite, false);

            // A faded sprite's owning view can be freed out from under this tracking set
            // between frames (e.g. a corpse mid-fade gets buried and its PersonView -
            // including every layer, not just the one that happened to be occluding -
            // is queued free) - nothing left to reset the alpha on, and touching it at all
            // throws (ObjectDisposedException), which previously broke out of _Process
            // every frame afterward and silently stalled ticks (input events fire through a
            // separate path, so clicking still worked while nothing else did).
            if (IsInstanceValid(sprite))
            {
                SetSpriteAlpha(sprite, 1f);
            }
        }
    }

    // Iterates BillboardSprite.LiveSprites (every billboard that currently exists, self
    // maintained - see its own doc comment) rather than scanning the scene tree - this used
    // to be a FindChildren("*", nameof(Sprite3D), recursive: true) walk of the *entire* tree,
    // called every single frame, which was fine back when decorations were a few thousand
    // purely-visual sprites with no collision/logic attached but became a severe per-frame
    // cost once they became real ResourceNode entities each with their own Area3D/collision
    // subtree to also walk past (see MapLoader.ScatterDecorations) - reads to the player as
    // the occlusion fade (and everything sharing the same _Process frame budget, camera
    // included) stuttering/blinking rather than as a slow scan. Ground shadow decals need no
    // exclusion here since they're never billboards to begin with (GroundShadow builds its
    // own plain Sprite3D, never through BillboardSprite.Create) - only the selection's own
    // sprites (which sit at the target position itself, not in front of it) still need one.
    private HashSet<Sprite3D> ComputeOccludingSprites()
    {
        var result = new HashSet<Sprite3D>();

        Vector3 targetPosition;
        Node? selectedPersonNode = null;
        if (_selectedPerson is { } person && _presenter.GetPersonGlobalPosition(person.Id) is { } personPosition)
        {
            targetPosition = personPosition;
            selectedPersonNode = _presenter.GetPersonNode(person.Id);
        }
        else
        {
            // Nobody selected - fall back to wherever the camera is actually looking (its
            // orbit/pan target), so something standing in front of the view doesn't get to
            // block it indefinitely just because no one happens to be selected right now.
            targetPosition = _cameraRig.RigGlobalPosition;
        }

        if (SightLine.From(_cameraRig.CameraGlobalPosition, targetPosition) is not { } sightLine)
        {
            return result;
        }

        foreach (var sprite in BillboardSprite.LiveSprites)
        {
            if (selectedPersonNode is not null && selectedPersonNode.IsAncestorOf(sprite))
            {
                continue;
            }

            // A tree's trunk layer (see ResourceNodeView) - Camera.png's "trunks stay
            // solid" rule: it should never fade just because a canopy elsewhere is worth
            // ghosting, even when it geometrically sits in the way itself.
            if (BillboardSprite.IsExcludedFromOcclusionFade(sprite))
            {
                continue;
            }

            // How wide the sprite actually renders, scale included - the same one place that
            // answers it for pixel-accurate picking (BillboardUv.RenderedSize), rather than
            // assuming a square canvas at its authored size the way this used to.
            var texture = sprite.Texture!;
            var scale = sprite.GlobalTransform.Basis.Scale;
            var renderedWidth = BillboardUv.RenderedSize(sprite.PixelSize, texture.GetWidth(), texture.GetHeight(), scale.X, scale.Y).X;

            if (sightLine.IsBlockedBy(
                    sprite.GlobalPosition,
                    renderedWidth / 2f,
                    _presentation.OcclusionMargin,
                    _presentation.OcclusionDistanceTolerance))
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

    // A 2D screen-space overlay, not a 3D billboard - see
    // PresentationSettings.SelectionMarkerScreenSize's doc comment for why.
    // Camera3D.UnprojectPosition/IsPositionBehind do the actual perspective math; this just
    // anchors a plain Control on top of that one projected point.
    private void UpdateSelectionMarkerOverlay()
    {
        if (_selectedPerson is not { } person
            || _presenter.GetPersonGlobalPosition(person.Id) is not { } personPosition
            || _presenter.GetPersonHeadHeightOffset(person.Id) is not { } headHeightOffset)
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
            screenPosition.Y - _presentation.SelectionMarkerScreenGap - _selectionMarkerOverlay.Size.Y);
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
        _terrain = TerrainSetup.Create(this, OnGroundInputEvent);
    }

    private void SetUpCamera()
    {
        var campX = (float)_campCenter.X;
        var campZ = (float)_campCenter.Y;
        var campPosition = new Vector3(campX, _terrain.SampleHeight(campX, campZ), campZ);
        _cameraRig = new FreeCameraRig(
            this,
            campPosition,
            _presentation.InitialZoomDistance,
            _presentation.MinZoom,
            _presentation.MaxZoom,
            _terrain.SampleHeight);
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
            Size = new Vector2(_presentation.SelectionMarkerScreenSize, _presentation.SelectionMarkerScreenSize),
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
            // A Theme resource cascades its DefaultFontSize down to every descendant Control
            // that doesn't set its own override - unlike AddThemeFontSizeOverride, which only
            // affects the single Control it's called on - so this alone shrinks the title,
            // every label, and every contextual button inside.
            Theme = new Theme { DefaultFontSize = _presentation.InspectorFontSize },
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

        // A development view, not a gameplay one (see RevealableExploration): the whole map
        // as if fog of war did not exist. Sits with "Spawn Person" rather than in
        // _contextualActions because it has nothing to do with whoever is selected.
        var revealMapToggle = new CheckButton { Text = "Reveal Map" };
        revealMapToggle.Toggled += OnRevealMapToggled;
        panel.Body.AddChild(revealMapToggle);

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

    // Refreshes right away rather than waiting for the next tick: the tick interval is long
    // enough that a toggle which only took effect a moment later would read as broken.
    private void OnRevealMapToggled(bool toggledOn)
    {
        _exploration.RevealAll = toggledOn;
        _presenter.RefreshExploration();
        _fogOfWar.Refresh();
        _groundClouds.Refresh();
    }

    private void OnSpawnButtonPressed()
    {
        var name = PersonNames.Pool[Random.Shared.Next(PersonNames.Pool.Length)];
        _world.Execute(new SpawnPersonCommand(name, FindFreeSpawnPosition(), Person.Unknown, Person.Unknown));
    }

    private void OnCraftButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then craft.");
            return;
        }

        _world.Execute(new CraftCommand(person, new ItemKindId("axe")));
        RefreshInfoLabel();
    }

    private void OnCraftClothingButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then craft.");
            return;
        }

        _world.Execute(new CraftCommand(person, new ItemKindId("warm_clothing")));
        RefreshInfoLabel();
    }

    private void OnCraftBasketButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then craft.");
            return;
        }

        _world.Execute(new CraftCommand(person, new ItemKindId("basket")));
        RefreshInfoLabel();
    }

    private void OnCraftBagButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then craft.");
            return;
        }

        _world.Execute(new CraftCommand(person, new ItemKindId("bag")));
        RefreshInfoLabel();
    }

    private void OnBuildButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then build.");
            return;
        }

        var buildPosition = FindFreeBuildingPosition(person.Position);
        _world.Execute(new ConstructCommand(person, new BuildingKindId("storage_hut"), buildPosition));
        RefreshInfoLabel();
        RefreshBuildingsLabel();
    }

    private void OnRepairButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then repair.");
            return;
        }

        var nearestBuilding = FindNearestBuilding(person.Position);
        if (nearestBuilding is null)
        {
            _statusBar.Notify("No buildings to repair yet.");
            return;
        }

        if (!_world.IsWithinReach(person.Position, nearestBuilding.Position))
        {
            _statusBar.Notify("The nearest building is too far away.");
            return;
        }

        _world.Execute(new RepairCommand(person, nearestBuilding));
        RefreshInfoLabel();
        RefreshBuildingsLabel();
    }

    private void OnDepositButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then deposit.");
            return;
        }

        var nearestBuilding = FindNearestBuilding(person.Position);
        if (nearestBuilding is null)
        {
            _statusBar.Notify("No buildings to deposit into yet.");
            return;
        }

        if (!_world.IsWithinReach(person.Position, nearestBuilding.Position))
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

        _world.Execute(new DepositCommand(person, nearestBuilding, woodItem, amount));
        RefreshInfoLabel();
        RefreshBuildingsLabel();
    }

    private void OnWithdrawButtonPressed()
    {
        const int withdrawAmount = 20;

        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then withdraw.");
            return;
        }

        var nearestBuilding = FindNearestBuilding(person.Position);
        if (nearestBuilding is null)
        {
            _statusBar.Notify("No buildings to withdraw from yet.");
            return;
        }

        if (!_world.IsWithinReach(person.Position, nearestBuilding.Position))
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

        _world.Execute(new WithdrawCommand(person, nearestBuilding, woodItem, amount));
        RefreshInfoLabel();
        RefreshBuildingsLabel();
    }

    private void OnFellButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then fell.");
            return;
        }

        var node = FindNearestFellableResourceNode(person.Position);
        if (node is null)
        {
            _statusBar.Notify("No trees nearby to fell.");
            return;
        }

        if (!_world.IsWithinReach(person.Position, node.Position))
        {
            _statusBar.Notify("The nearest tree is too far away.");
            return;
        }

        TeachBaseTechniqueIfNeeded(person, _world.Configuration.ResourceCatalog.Get(node.Kind).Skill);
        _world.Execute(new FellCommand(person, node));
        _presenter.RemoveResourceNodeView(node.Id);
        RefreshInfoLabel();
    }

    private void OnBuryButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then bury.");
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

        if (!_world.IsWithinReach(person.Position, deceased.Position))
        {
            _statusBar.Notify("The nearest deceased person is too far away.");
            return;
        }

        _world.Execute(new BuryCommand(person, deceased));
        _presenter.RemovePersonView(deceased.Id);
        RefreshInfoLabel();
        RefreshGravesLabel();
    }

    private void OnLootButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then loot.");
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

        if (!_world.IsWithinReach(person.Position, deceased.Position))
        {
            _statusBar.Notify("The nearest belongings are too far away.");
            return;
        }

        _world.Execute(new LootCommand(person, deceased));
        RefreshInfoLabel();
    }

    // Eats from whatever food kinds are on hand - gathering food only fills the inventory now
    // (see GatherCommand), so a person never gets fed without this. Tries every kind currently
    // carried rather than requiring the player to pick one; EatCommand itself no-ops for any
    // kind that isn't food, so this is safe to call across the whole inventory.
    private void OnEatButtonPressed()
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then eat.");
            return;
        }

        TeachBaseTechniqueIfNeeded(person, EatCommand.Skill);
        foreach (var item in person.Inventory.Counts.Keys.ToList())
        {
            if (person.Needs.Hunger <= 0f)
            {
                break;
            }

            _world.Execute(new EatCommand(person, item));
        }

        RefreshInfoLabel();
    }

    private Building? FindNearestBuilding(Position position) =>
        _world.Buildings.OrderBy(b => WorldState.Distance(b.Position, position)).FirstOrDefault();

    private ResourceNode? FindNearestFellableResourceNode(Position position) =>
        _world.ResourceNodes
            .Where(n => n.IsAlive && _world.Configuration.ResourceCatalog.Get(n.Kind).CanFell)
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
        var spread = _world.Configuration.Rules.MaxInteractionDistance;
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

    private void OnPersonClicked(Person person, MouseButton button)
    {
        if (button == MouseButton.Right)
        {
            TeachFromSelectedPersonTo(person);
            return;
        }

        _selectedPerson = person;
        _selectedGrave = null;
        _contextualActions.Visible = true;
        RefreshInfoLabel();
    }

    private void OnGraveSelected(Grave grave)
    {
        _selectedGrave = grave;
        _selectedPerson = null;
        _contextualActions.Visible = false;
        RefreshInfoLabel();
    }

    private void TeachFromSelectedPersonTo(Person student)
    {
        if (_selectedPerson is not { } teacher || teacher == student)
        {
            return;
        }

        // Directing a person to teach at all is the player showing them how to teach in the
        // first place - same as TeachBaseTechniqueIfNeeded for gather/fell/eat.
        TeachBaseTechniqueIfNeeded(teacher, TeachCommand.TeachingSkill);

        foreach (var technique in teacher.KnownTechniques)
        {
            _world.Execute(new TeachCommand(teacher, student, technique));
        }

        RefreshInfoLabel();
    }

    private void OnResourceNodeSelected(ResourceNode node)
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then click a resource node to gather.");
            return;
        }

        if (!_world.IsWithinReach(person.Position, node.Position))
        {
            _pendingGathers[person] = node;
            // Fully qualified: inside a Node3D, a bare `Position` is the node's own Vector3.
            _world.Execute(new MoveCommand(person, Core.World.Position.Approach(person.Position, node.Position, _presentation.ApproachDistance)));
            RefreshInfoLabel();
            return;
        }

        _pendingGathers.Remove(person);
        GatherFrom(person, node);
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

        foreach (var (person, node) in _pendingGathers.ToList())
        {
            if (!person.IsAlive)
            {
                _pendingGathers.Remove(person);
                continue;
            }

            if (!_world.IsWithinReach(person.Position, node.Position))
            {
                continue;
            }

            _pendingGathers.Remove(person);
            GatherFrom(person, node);
        }
    }

    // Depleting a node down to zero doesn't remove its view - the plant/tree is still there,
    // just fruitless until RegenPerTick brings it back. Only IsAlive turning false (felled or
    // withered - see FellCommand, WorldState.Advance) means the thing itself is actually gone.
    private void GatherFrom(Person person, ResourceNode node)
    {
        TeachBaseTechniqueIfNeeded(person, _world.Configuration.ResourceCatalog.Get(node.Kind).Skill);
        _world.Execute(new GatherCommand(person, node));
    }

    // Nobody starts knowing anything (see SkillDefinition.BaseTechnique) - the player directing
    // an action at all is how "God" shows a person the way, so every player-driven action that
    // needs a skill grants its base technique first if the selected person doesn't have it yet,
    // rather than silently no-oping or requiring a separate "teach" step beforehand.
    private void TeachBaseTechniqueIfNeeded(Person person, SkillTypeId skill)
    {
        var baseTechnique = _world.Configuration.SkillCatalog.Get(skill).BaseTechnique;
        if (!person.KnownTechniques.Contains(baseTechnique))
        {
            _world.Execute(new GrantTechniqueCommand(person, baseTechnique));
        }
    }

    // By the time a view forwards here, it has already tried HoverRescue.TryClickElsewhere
    // itself - a full re-cast of the same ray, excluding whatever's already been ruled out,
    // checking every other real candidate actually along it (see that doc comment). Nothing
    // along the ray panned out, so this genuinely is a ground click (or a click into empty
    // space with nothing real anywhere near it) - a plain move order is the correct read, not
    // a guess.
    //
    // The position the view hands over is the ray's hit on the view's own collision box, not
    // on the ground - a point up in the air on the front face of a tree-sized box, whose X/Z
    // can be tens of meters off from the ground the cursor is actually over (see GroundPick).
    // Only the screen position is reused; where the ground really is under it is re-derived.
    // A click that finds no ground at all (into the sky past the terrain's edge) is dropped
    // rather than guessed at.
    private void OnMissedClick(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (camera is not Camera3D camera3D
            || @event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouseButton)
        {
            return;
        }

        if (GroundPick.FindGround(camera3D, mouseButton.Position) is { } groundPosition)
        {
            OrderWalkTo(groundPosition);
        }
    }

    private void OnGroundInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            OrderWalkTo(position);
        }
    }

    private void OrderWalkTo(Vector3 groundPosition)
    {
        if (_selectedPerson is not { } person)
        {
            _statusBar.Notify("Select a person first, then click the ground to walk there.");
            return;
        }

        _world.Execute(new MoveCommand(person, WorldSpace.ToSimulation(groundPosition)));
        RefreshInfoLabel();
    }

    private void RefreshInfoLabel()
    {
        if (_selectedGrave is { } grave)
        {
            _infoLabel.Text = InspectorText.ForGrave(grave);
            return;
        }

        if (_selectedPerson is not { } person)
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
        var carriedWeight = person.Inventory.TotalWeight(_world.Configuration.ItemCatalog);
        var maxCarryWeight = _world.MaxCarryWeightFor(person);
        _infoLabel.Text =
            $"{person.Id}  {person.Name}{status}\n" +
            $"Position: {person.Position}\n" +
            $"Age: {AgeText(person)}\n" +
            $"Task: {InspectorText.ForTask(person)}\n" +
            $"Hunger: {person.Needs.Hunger}  Fatigue: {person.Needs.Fatigue}\n" +
            $"Skills: {skills}\n" +
            $"Known techniques: {techniques}\n" +
            $"Carrying: {carriedWeight}/{maxCarryWeight}\n" +
            $"Inventory: {inventory}";
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
