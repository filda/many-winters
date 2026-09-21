using Godot;
using ManyWinters.Core.Commands;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Maps;
using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
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

    private FloatingPanel _inspector = null!;
    private Label _infoLabel = null!;
    private Label _buildingsLabel = null!;
    private Label _gravesLabel = null!;
    private SelectionPanel _selectionPanel = null!;
    private StatusBar _statusBar = null!;
    private InscriptionOverlay _inscriptionOverlay = null!;
    private PausePanel _pausePanel = null!;
    private HelpPanel _helpPanel = null!;
    private ChroniclePanel _chronicle = null!;
    private WorkshopPanel _workshop = null!;

    // What keeps the rest of the screen out of reach while the workbench is out (SetUpWorkshop).
    private Control _workshopShield = null!;
    private BandPanel _bandPanel = null!;
    private ContextMenu _contextMenu = null!;
    private EndingAnnouncements _endingAnnouncements = new();
    private TextureRect _selectionMarkerOverlay = null!;
    // The Person itself, not an id: commands and labels want the object and PersonView hands it
    // over on click, so nothing is looked up between "clicked" and "acted on".
    private Person? _selectedPerson;
    private Grave? _selectedGrave;
    private double _tickAccumulator;

    // Skips ComputeOccludingSprites (an O(sprite count) scan) on frames where neither the camera
    // nor the occlusion target moved: with the camera at rest and nobody selected walking, the
    // sight line - and so the occluding set - cannot have changed since last frame.
    private Vector3? _lastOcclusionCameraPosition;
    private Vector3? _lastOcclusionTargetPosition;
    private const float OcclusionRecomputeDistanceSquaredThreshold = 0.0001f;

    // Captured in _Ready, the one moment BandArrival.Of really means "just arrived"; TogglePause
    // calls BandArrival.Of again later only for its live population counts.
    private long _bandArrivalTick;

    // Orders given to somebody who first has to walk there; ResolvePendingOrders fires each one
    // on arrival, so pointing at something across the clearing means "go and do that" instead of
    // a greyed-out line telling the player to walk them over themselves.
    private readonly PendingOrders _pendingOrders = new();

    // Telling a right-click apart from the right-drag that turns the camera, and what the press
    // landed on until the button comes up (see HandleRightButton). The world's views report the
    // press; only the release decides whether a menu opens.
    private readonly RightClickGesture _rightClick = new();
    private Func<Person, TargetMenu>? _pointedAt;

    // The one thing the game says out loud about an order nobody can carry out. Every order the
    // player gives goes through Acting(), so it is said in exactly one way.
    private const string NobodySelected = "Select someone first, then tell them what to do.";

    // Above the game's own UI canvas (SetUpUi), which is built three quarters of the way through
    // the load: both sit on the default layer otherwise, and the status bar - added to the tree
    // later - drew over the bottom of the title page for the rest of the load.
    private const int LoadingCanvasLayer = 100;

    // The title page, held up while the world is built. On its own CanvasLayer so it covers the
    // game's own UI layer, and freed rather than hidden once there is a world to look at.
    private CanvasLayer? _loadingCanvas;
    private LoadingScreen? _loadingScreen;

    // Nothing below is built yet: the camera, the presenter and the terrain are all still null,
    // and every per-frame and input path that touches them has to stand down until they are not.
    private bool _loading;

    // Async so the loading screen can be drawn between the steps: without yielding a frame, all
    // of this runs inside one frame and the player sees a frozen window and then a world.
    public override async void _Ready()
    {
        _loading = true;

        _loadingCanvas = new CanvasLayer { Layer = LoadingCanvasLayer };
        AddChild(_loadingCanvas);
        _loadingScreen = new LoadingScreen();
        _loadingCanvas.AddChild(_loadingScreen);

        await Building(0, "Reading the winter's rules");
        var configuration = WorldConfiguration.LoadFromJson(catalog => ContentFiles.ReadJsonTree($"res://Content/{catalog}"));

        await Building(10, "Raising the land");
        var map = MapLoader.LoadDefault(configuration);
        _world = map.World;

        // Everyone idles for up to IdleTask.MaxPauseTicks before their first wander leg, and with
        // the prologue holding the clock a band that then stood still read as stuck. Running those
        // ticks before the views exist keeps the same deterministic world, watched from a few
        // ticks in; it costs the band that much hunger before the player can act.
        await Building(35, "Waking the band");
        _world.Advance(IdleTask.MaxPauseTicks + 1);

        await Building(45, "Hanging the sky");
        _exploration = new RevealableExploration(_world.Exploration);
        _campCenter = map.CampCenter;
        GetViewport().PhysicsObjectPicking = true;
        SetUpLighting();
        SetUpSky();

        await Building(55, "Laying the ground");
        SetUpTerrain();

        await Building(70, "Placing the camera");
        SetUpCamera();

        await Building(75, "Drawing the pages");
        SetUpUi();

        await Building(85, "Gathering the clouds");
        CloudScatter.Scatter(this, _terrain.Half);
        _cloudFogMask = new CloudFogMask(this, _cameraRig.Camera);

        await Building(90, "Setting out the band");
        _presenter = new WorldPresenter(this, _world, _exploration, _cameraRig.RigGlobalPosition, _cameraRig.ViewRadius, OnPersonClicked, OnResourceNodeClicked, OnBuildingClicked, OnGraveSelected, OnItemPileClicked, OnMissedClick, _terrain.SampleHeight);
        _fogOfWar = new FogOfWarRenderer(_exploration, _terrain.Half, _cameraRig.Camera, _cloudFogMask);
        _groundClouds = new GroundClouds(this, _fogOfWar, _terrain.Half, _terrain.SampleHeight);

        await Building(100, "The band arrives");
        var arrival = BandArrival.Of(_world);
        _bandArrivalTick = arrival.ArrivalTick;

        _loadingCanvas.QueueFree();
        _loadingCanvas = null;
        _loadingScreen = null;
        _loading = false;

        ShowInscription(Prologue.Write(arrival), offerAnotherBand: false);

        GD.Print($"Main ready. World has {_world.People.Count} people and {_world.Entities.Count(e => e.Category == EntityCategory.Growable)} resource nodes at tick {_world.Clock.CurrentTick}.");
        // Answers "am I running the build I think I am" (a stale process after hot-reload or a
        // forgotten relaunch) with one log line; derived from the assembly, not bumped by hand.
        GD.Print($"Build tag: {BuildTag.For(AssemblyBuildTimeUtc())}");
    }

    // Says what is about to be built, then lets the frame draw before building it - the other way
    // round and every line the player reads names the step that has just finished.
    private async Task Building(float progress, string step)
    {
        _loadingScreen!.Show(progress, step);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    // Last write time of the running assembly - a build stamp needing no build-time code
    // generation. Built from BaseDirectory because Assembly.Location is empty here: Godot loads
    // the assembly from a stream so the file can be overwritten while the editor holds it. Null
    // when there is nothing to stat; BuildTag renders that as "unknown" rather than a guess.
    private static DateTimeOffset? AssemblyBuildTimeUtc()
    {
        var assemblyPath = Path.Combine(AppContext.BaseDirectory, $"{typeof(Main).Assembly.GetName().Name}.dll");

        return File.Exists(assemblyPath)
            ? new DateTimeOffset(File.GetLastWriteTimeUtc(assemblyPath), TimeSpan.Zero)
            : null;
    }

    public override void _Process(double delta)
    {
        // Half the world does not exist yet; every line below reaches into it.
        if (_loading)
        {
            return;
        }

        _cameraRig.HandleInput((float)delta);
        // Every frame, not per tick: the camera and the selected person's interpolated position
        // move continuously between ticks, so what stands in the way changes continuously too.
        UpdateOcclusionFade();
        UpdateSelectionMarkerOverlay();
        // Also every frame: hover is taken on mouse movement but can be lost without any - a
        // person can walk out from under a resting cursor (see HoverArbiter).
        _presenter.RevalidateHover();
        // Also every frame: the mask camera tracks the main camera's continuous movement.
        _cloudFogMask.Update();

        // Time stands still while an inscription is up: what it says is true of this moment, and
        // the player decides when the world moves on (at the start, a chance to look around before
        // hunger counts). A pause the player asked for (TogglePause) holds the clock the same way,
        // and so does the controls page - it is read instead of playing, not while playing.
        if (_inscriptionOverlay.Visible || _pausePanel.Visible || _helpPanel.Visible || _workshop.Visible)
        {
            return;
        }

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
        _presenter.RefreshExploration(_cameraRig.RigGlobalPosition, _cameraRig.ViewRadius);
        _fogOfWar.Refresh();
        _groundClouds.Refresh();
        ResolvePendingOrders();
        _statusBar.SetTick(_world.Clock.CurrentTick, _world.CurrentSeason);
        RefreshSelection();
        RefreshBuildingsLabel();
        RefreshGravesLabel();
        AnnounceEndingIfAny();

        foreach (var person in _world.People)
        {
            _presenter.SetPersonAlive(person.Id, person.IsAlive);
            // A person who dies mid-stride still tweens to that tick's final position over the
            // next second - one last visible step. Snapping (overSeconds: 0) once dead pins the
            // corpse there with nothing left to glide.
            _presenter.SetPersonPosition(person.Id, person.Position, person.IsAlive ? (float)_pacing.TickIntervalSeconds : 0f);
        }

        foreach (var node in _world.Entities)
        {
            if (node.Growth is not { } growth)
            {
                continue;
            }

            if (!growth.IsAlive)
            {
                // Nodes that withered from climate stress (see WorldState.Advance); felling
                // removes its own view immediately.
                _presenter.RemoveResourceNodeView(node.Id);
                continue;
            }

            _presenter.SetResourceNodeHasFruit(node.Id, growth.RemainingAmount > 0);
        }

        GD.Print($"Tick {_world.Clock.CurrentTick}: {_world.People.Count(p => p.IsAlive)} of {_world.People.Count} people alive.");
    }

    public override void _Input(InputEvent @event)
    {
        // Nothing to toggle, pause or dismiss until _Ready has finished building it.
        if (_loading)
        {
            return;
        }

        // A name for a new thing is being typed (see TextEntry): every letter belongs to the
        // field, so the keys the game answers to on its own are left alone. Escape and F11 are
        // not letters and still work - one puts the workbench away, the other is the window's.
        var typing = TextEntry.HasTheKeyboard(GetViewport());

        if (!typing && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.T })
        {
            _cameraRig.ToggleProjection();
        }

        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F11 })
        {
            ToggleFullscreen();
        }

        if (!typing && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space })
        {
            // Space is Godot's default ui_accept: unless eaten here it also activates whichever
            // Control last took focus (Chronicle, the "?" button) on top of toggling the pause.
            TogglePause();
            GetViewport().SetInputAsHandled();
        }

        // Nothing else answers to Escape, and a menu or a page that can only be dismissed by
        // clicking one particular thing is one the player fights. Both, not one or the other:
        // the controls page swallows the clicks that would open a menu, so only one can be up.
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            _contextMenu.Close();
            _workshop.Close();
            if (_helpPanel.Visible)
            {
                _helpPanel.Dismiss();
            }
        }

        HandleRightButton(@event);

        // Ahead of Godot's physics picking (which runs later, from unhandled input) so it wins even
        // when the pick would land on something opaque in front of a person - see
        // PresentationSettings.PersonClickScreenRadius.
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouseButton
            && GetViewport().GuiGetHoveredControl() is null)
        {
            // A click out in the world puts the menu away, as any menu closes when the player
            // looks elsewhere. Only out in the world: over the UI the press has to reach whatever
            // it landed on, and a button of the menu's own only fires when it comes back up.
            _contextMenu.Close();

            if (FindNearestPersonOnScreen(mouseButton.Position) is { } person)
            {
                OnPersonClicked(person, MouseButton.Left);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    // F11 moves between the window and a borderless fullscreen - the whole screen, taskbar
    // included, with no native Windows chrome (WindowMode.Fullscreen rather than the exclusive
    // video-mode switch, which is the less forgiving kind on Windows). F alone zooms the camera
    // (FreeCameraRig), so the key is F11; the controls page lists it under Windows.
    private static void ToggleFullscreen()
    {
        var mode = DisplayServer.WindowGetMode();
        var inFullscreen = mode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
        DisplayServer.WindowSetMode(inFullscreen ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
    }

    // The right button does two jobs: dragged it turns the camera (FreeCameraRig), pressed and
    // released in one spot it asks what may be done with whatever is under the cursor. So the
    // menu waits for the release (RightClickGesture), and what the cursor was over is recorded on
    // the press - the only half of it a view ever sees, since Godot delivers presses to colliders
    // through physics picking, which runs after this.
    private void HandleRightButton(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } pressed:
                _pointedAt = null;
                _contextMenu.Close();
                _rightClick.Press(pressed.Position);
                break;
            case InputEventMouseMotion motion:
                _rightClick.Moved(motion.Position);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false } released:
                if (_rightClick.Release())
                {
                    ShowContextMenu(released.Position);
                }

                break;
        }
    }

    // Opens the menu for whatever the press landed on. An empty one is not opened at all: the one
    // target with nothing to offer is the selected person themselves, whose own card is already on
    // screen (see TargetActions).
    private void ShowContextMenu(Vector2 screenPosition)
    {
        if (_pointedAt is not { } menuFor || Acting() is not { } person)
        {
            return;
        }

        var menu = menuFor(person);
        if (menu.Offers.Count > 0)
        {
            _contextMenu.Open(menu.Heading, menu.Offers, screenPosition);
        }
    }

    // Whoever an order is for. Every order the player gives needs somebody to carry it out, and
    // this is the one place that says so when there is nobody.
    private Person? Acting()
    {
        if (_selectedPerson is { } person)
        {
            return person;
        }

        _statusBar.Notify(NobodySelected);
        return null;
    }

    // The already-selected person is never a candidate: re-selecting is a no-op, and the radius
    // around them swallowed every "step over there" click on the ground at their feet. Excluded,
    // the click falls through to picking; their own opaque pixels still re-select them.
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

    // _UnhandledInput, not _Input: _Input fires before the UI gets the event, so wheel/drag over
    // a Control would also zoom/rotate the camera underneath. That alone is not enough - a
    // ScrollContainer with nothing left to scroll lets the wheel fall through - so the camera also
    // ignores everything while the cursor is over any Control at all.
    public override void _UnhandledInput(InputEvent @event)
    {
        // The camera rig is built two thirds of the way through the load, and until then one
        // mouse movement over the title page was enough to throw here every frame.
        if (_loading)
        {
            return;
        }

        if (GetViewport().GuiGetHoveredControl() is not null)
        {
            return;
        }

        _cameraRig.HandleMouseInput(@event);
    }

    // A decoration sprite between the camera and the selected person would otherwise hide them
    // with no way to tell where they went.
    private void UpdateOcclusionFade()
    {
        var (targetPosition, selectedPersonNode) = ResolveOcclusionTarget();
        var cameraPosition = _cameraRig.CameraGlobalPosition;

        if (_lastOcclusionCameraPosition is { } lastCameraPosition
            && _lastOcclusionTargetPosition is { } lastTargetPosition
            && cameraPosition.DistanceSquaredTo(lastCameraPosition) < OcclusionRecomputeDistanceSquaredThreshold
            && targetPosition.DistanceSquaredTo(lastTargetPosition) < OcclusionRecomputeDistanceSquaredThreshold)
        {
            return;
        }

        _lastOcclusionCameraPosition = cameraPosition;
        _lastOcclusionTargetPosition = targetPosition;

        var occluding = ComputeOccludingSprites(cameraPosition, targetPosition, selectedPersonNode);

        // Re-applied every frame, not only on entering the set: the hover highlight rewrites the
        // same sprite's Modulate on every hover-state change and would undo the fade whenever the
        // cursor sits on an occluding canopy. Cheap - the set is a handful of sprites.
        // The faded set lives in BillboardSprite because picking consults it too (see
        // BillboardSprite.OcclusionFadedSprites); this is still the only place deciding membership.
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

            // The owning view can be freed between frames (a corpse mid-fade gets buried and its
            // PersonView queued free); touching a freed sprite throws ObjectDisposedException,
            // which breaks out of _Process every frame and silently stalls ticks.
            if (IsInstanceValid(sprite))
            {
                SetSpriteAlpha(sprite, 1f);
            }
        }
    }

    // What the occlusion sight line runs to: the selected person if any (and the node to exclude,
    // since it sits at the target itself), else the camera's own orbit/pan target so nothing gets
    // to block the view indefinitely just because no one is selected.
    private (Vector3 TargetPosition, Node? SelectedPersonNode) ResolveOcclusionTarget()
    {
        if (_selectedPerson is { } person && _presenter.GetPersonGlobalPosition(person.Id) is { } personPosition)
        {
            return (personPosition, _presenter.GetPersonNode(person.Id));
        }

        return (_cameraRig.RigGlobalPosition, null);
    }

    // Walks BillboardSprite.LiveSprites rather than the scene tree: a per-frame FindChildren over
    // every ResourceNode's Area3D subtree stuttered the whole frame, camera included. Ground
    // shadows are plain Sprite3Ds (GroundShadow), never billboards, so need no exclusion; only
    // the selection's own sprites do, since they sit at the target itself.
    private HashSet<Sprite3D> ComputeOccludingSprites(Vector3 cameraPosition, Vector3 targetPosition, Node? selectedPersonNode)
    {
        var result = new HashSet<Sprite3D>();

        if (SightLine.From(cameraPosition, targetPosition) is not { } sightLine)
        {
            return result;
        }

        foreach (var sprite in BillboardSprite.LiveSprites)
        {
            if (selectedPersonNode is not null && selectedPersonNode.IsAncestorOf(sprite))
            {
                continue;
            }

            // A tree's trunk layer (see ResourceNodeView): Camera.png's "trunks stay solid" rule,
            // even when the trunk geometrically sits in the way itself.
            if (BillboardSprite.IsExcludedFromOcclusionFade(sprite))
            {
                continue;
            }

            // Rendered width, scale included - the same answer pixel-accurate picking uses
            // (BillboardUv.RenderedSize), not the authored canvas size.
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

    // A 2D overlay, not a 3D billboard (see PresentationSettings.SelectionMarkerScreenSize).
    // Camera3D.UnprojectPosition/IsPositionBehind do the projection; this anchors a Control on it.
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

        // SelectionMarkerScreenGap is screen pixels, so it applies to the projected point, not to
        // headPosition before projecting.
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

    private void SetUpSky()
    {
        SkySetup.Create(this);
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

        // The status bar first: it carries the buttons the windows below hang their own toggles on.
        SetUpStatusBar(canvas);
        SetUpInspectorWindow(canvas);
        SetUpSelectionMarker(canvas);
        SetUpSelectionPanel(canvas);
        SetUpBandPanel(canvas);
        SetUpChronicle(canvas);
        SetUpWorkshop(canvas);
        SetUpContextMenu(canvas);
        SetUpInscriptionOverlay(canvas);
        SetUpPausePanel(canvas);
        SetUpHelpPanel(canvas);
    }

    // The workbench, opened from the pack line on the selected person's card. Like the pause
    // page it holds the clock while it is up (see _Process): working a thing over is meant to be
    // unhurried.
    private void SetUpWorkshop(CanvasLayer canvas)
    {
        // Laid in before the workbench, so it sits under it and over everything added earlier -
        // the roster, the selected person's card, the status bar, the world itself. The clock is
        // stopped while the bench is out, and an order given into a stopped clock lands the
        // moment it starts again (the same reasoning as InscriptionOverlay). It draws nothing:
        // the world is what the player is working in the middle of, and the camera keeps turning
        // over it.
        _workshopShield = new Control { MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        _workshopShield.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        canvas.AddChild(_workshopShield);

        _workshop = new WorkshopPanel();
        // Tied to the panel itself rather than to the places that open and close it, of which
        // there are several (the pack line, Escape, the way out of the panel).
        _workshop.VisibilityChanged += () => _workshopShield.Visible = _workshop.Visible;
        // Letting it go primes the tick accumulator, so the world starts again on the next frame
        // rather than a full interval later - as dismissing the controls page does.
        _workshop.Closed += () => _tickAccumulator = _pacing.TickIntervalSeconds;
        _workshop.Attempted += OnWorkshopAttempt;
        _workshop.Named += OnWorkshopNamed;
        _workshop.PickChanged += RefreshWorkshopOffer;
        _workshop.RecipeInvoked += OnWorkshopRecipe;
        canvas.AddChild(_workshop);
    }

    private void OpenWorkshop()
    {
        // Pressing the pack line again puts the workbench away: the way in is the way out.
        if (_workshop.Visible)
        {
            _workshop.Close();
            return;
        }

        if (_selectedPerson is not { } person)
        {
            return;
        }

        // It puts itself in the middle of the screen and stays there (FloatingPanel.KeepCentred):
        // the world stands still while this is open, so it is the thing being done rather than a
        // card to read beside it.
        _workshop.Open(WorkshopActions.Carried(_world, person), WorkshopActions.Recipes(_world, person));
        RefreshWorkshopOffer();
    }

    // Pressed a "Make X" line rather than picked something to try - the recipe list has its own
    // event because a successful one changes the pack the same attempt does, and the panel needs
    // both redrawn (WorkshopActions.Carried, WorkshopActions.Recipes).
    private void OnWorkshopRecipe(ActionOffer offer)
    {
        if (_selectedPerson is not { } person)
        {
            return;
        }

        Perform(person, offer);

        _workshop.Show(WorkshopActions.Carried(_world, person));
        _workshop.ShowRecipes(WorkshopActions.Recipes(_world, person));
        RefreshWorkshopOffer();
    }

    // What the current pick would do, asked of the world rather than of the panel: the panel
    // holds no world and no opinion about what works (see WorkshopActions).
    private void RefreshWorkshopOffer()
    {
        if (_selectedPerson is not { } person)
        {
            return;
        }

        var offer = WorkshopActions.Attempt(_world, person, _workshop.Picked);
        _workshop.Offer(offer, RefusalFor(offer), WorkshopActions.WordsFor(_world, person, _workshop.Picked));
    }

    // Nothing is said about a pick that leads nowhere until the player has picked something: an
    // empty workbench that already says "nothing comes of it" is answering a question nobody
    // asked.
    private string? RefusalFor(ActionOffer? offer) => (offer, _workshop.Picked.Count) switch
    {
        (null, 0) => null,
        (null, _) => "Nothing comes of it.",
        ({ IsAvailable: false }, _) => ActionBlockerText.For(offer.Value),
        _ => null,
    };

    private void OnWorkshopAttempt()
    {
        if (_selectedPerson is not { } person
            || WorkshopActions.Attempt(_world, person, _workshop.Picked) is not { } offer)
        {
            return;
        }

        var before = person.Inventory.Assemblies.ToList();
        Perform(person, offer);

        // An attempt costs time whether or not it came off - the clock is held while the bench is
        // open, so this is the only thing that moves it, and it is what stops a player pressing
        // until the dice land (see WorkAttempt, SimulationRules.TicksPerWorkAttempt).
        _world.Advance(_world.Configuration.Rules.TicksPerWorkAttempt);

        var made = person.Inventory.Assemblies.FirstOrDefault(held => !before.Remove(held));
        _workshop.Show(WorkshopActions.Carried(_world, person));
        _workshop.ShowRecipes(WorkshopActions.Recipes(_world, person));
        _workshop.ReportOutcome(made is null
            ? "It comes apart in your hands."
            : $"It comes out {InspectorText.ForWorkedThing(made, _world)}.");

        // A shape nobody in the band has a word for is a thing worth naming, and this is the
        // moment to ask: they are looking at what they just made (see Vocabulary).
        if (made is not null && !_world.Vocabulary.HasAWordFor(made))
        {
            _justMade = made;
            _workshop.AskForAName();
        }

        RefreshWorkshopOffer();
    }

    // What the last attempt turned out, held only long enough for the player to name it.
    private Core.Materials.Assembly? _justMade;

    private void OnWorkshopNamed(string word)
    {
        if (_justMade is not { } made)
        {
            return;
        }

        _world.Vocabulary.Name(made, word);
        _justMade = null;

        // A word the band coined outlives whoever coined it, so it goes in the chronicle rather
        // than only into the panel that asked for it.
        RecordInscription(new Inscription(
            "A name for it",
            [$"{_selectedPerson?.Name ?? "Somebody"} made a thing the band had no word for.", $"They are calling it {word}."],
            // Carried even though the chronicle leaves closing words off the page: an
            // inscription without them is one the overlay cannot be dismissed from, and that is
            // meant only for a band with nobody left (see InscriptionOverlay.Show).
            "The word is passed along"));

        _workshop.ReportOutcome($"They are calling it {word}.");

        if (_selectedPerson is { } person)
        {
            _workshop.Show(WorkshopActions.Carried(_world, person));
            _workshop.ShowRecipes(WorkshopActions.Recipes(_world, person));
        }
    }

    private void SetUpBandPanel(CanvasLayer canvas)
    {
        _bandPanel = new BandPanel();
        _bandPanel.PersonChosen += GoTo;
        canvas.AddChild(_bandPanel);
        _statusBar.BandRequested += ToggleBandPanel;
    }

    // Opened and closed by the button on the status bar.
    //
    // Placed on the way open rather than once at setup, so it always comes back where the player
    // expects it however far they dragged it last time: mirrored across the screen from the
    // selection panel, same inset from its own edge (see BandPanel), the band on the left and
    // whoever is picked out of it on the right.
    //
    // Filled on the way open as well as on every tick: the clock can be standing still (a pause,
    // an inscription), and an empty roster is no answer to "where is everybody".
    private void ToggleBandPanel()
    {
        _bandPanel.Visible = !_bandPanel.Visible;
        _bandPanel.Position = new Vector2(BandPanel.Margin, BandPanel.Margin);
        RefreshBandPanel();
    }

    private void RefreshBandPanel()
    {
        if (_bandPanel.Visible)
        {
            _bandPanel.Update(BandRoster.Of(_world));
        }
    }

    // Pressing a name on the roster: select the person and take the view to them. Selecting alone
    // would leave the player looking at the same empty forest with a marker somewhere off screen.
    private void GoTo(Person person)
    {
        _selectedPerson = person;
        _selectedGrave = null;
        RefreshSelection();

        if (_presenter.GetPersonGlobalPosition(person.Id) is { } position)
        {
            _cameraRig.FocusOn(position);
        }
    }

    // Opposite the inspector, so the two can be open at once without covering each other.
    private void SetUpChronicle(CanvasLayer canvas)
    {
        _chronicle = new ChroniclePanel
        {
            Position = new Vector2(GetViewport().GetVisibleRect().Size.X - 476f, 16f),
        };
        canvas.AddChild(_chronicle);
        _statusBar.ChronicleRequested += _chronicle.Toggle;
    }

    // After the windows, so a menu opened over one of them is on top of it; before the
    // inscription overlay and the pause panel, which are on top of everything.
    private void SetUpContextMenu(CanvasLayer canvas)
    {
        _contextMenu = new ContextMenu();
        _contextMenu.ActionInvoked += OnActionInvoked;
        canvas.AddChild(_contextMenu);
    }

    // Added after the contextual menu so it draws over everything else on the canvas, the
    // inspector included.
    private void SetUpInscriptionOverlay(CanvasLayer canvas)
    {
        _inscriptionOverlay = new InscriptionOverlay();
        // The clock stood still, so the next tick is due the moment the inscription comes down -
        // a full interval later read as the world taking a second to notice.
        _inscriptionOverlay.Dismissed += () => _tickAccumulator = _pacing.TickIntervalSeconds;
        _inscriptionOverlay.AnotherBandRequested += OnAnotherBandRequested;
        canvas.AddChild(_inscriptionOverlay);
    }

    // After the inscription overlay: the two never show at once today (ticking, and with it every
    // death, is on hold while either is up), but this is the one that should draw on top.
    private void SetUpPausePanel(CanvasLayer canvas)
    {
        _pausePanel = new PausePanel();
        // The cross on the page is the other half of Space: both let the world go again.
        _pausePanel.Resumed += TogglePause;
        canvas.AddChild(_pausePanel);
    }

    // Last of all, so the controls can be read over whatever else is up. Opened and closed by
    // the "?" on the status bar or by Escape; like an inscription being dismissed, letting it go
    // primes the tick accumulator so the world starts again on the next frame rather than a full
    // interval later.
    private void SetUpHelpPanel(CanvasLayer canvas)
    {
        _helpPanel = new HelpPanel();
        _helpPanel.Dismissed += () => _tickAccumulator = _pacing.TickIntervalSeconds;
        canvas.AddChild(_helpPanel);
        _statusBar.HelpRequested += _helpPanel.Toggle;
    }

    // Space toggles the clock at the player's request - ignored while an inscription holds it,
    // which is not the player's to override. Unpausing primes the tick accumulator like an
    // inscription dismissal does (SetUpInscriptionOverlay), so the world resumes next frame.
    private void TogglePause()
    {
        // Neither is the player's to override, and both already hold the clock: a pause asked for
        // behind a page nobody can see would only surface when that page comes down.
        if (_inscriptionOverlay.Visible || _helpPanel.Visible)
        {
            return;
        }

        if (_pausePanel.Visible)
        {
            _pausePanel.Hide();
            _tickAccumulator = _pacing.TickIntervalSeconds;
            return;
        }

        var band = BandArrival.Of(_world);
        var sinceArrival = DurationText.For(_world.Clock.CurrentTick - _bandArrivalTick, _world.Configuration.Rules.TicksPerYear, _world.Configuration.Rules.TicksPerSeason);
        _pausePanel.Show(band.BandName, sinceArrival, PopulationSummary.Of(band.People, band.Men, band.Women, band.Children));
    }

    // The fate is read off the world every tick and shown the first tick it changes (see
    // EndingAnnouncements): once when the last man or woman dies, once more when the last
    // person does.
    private void AnnounceEndingIfAny()
    {
        if (!_endingAnnouncements.ShouldAnnounce(BandEnding.FateOf(_world.People)))
        {
            return;
        }

        if (BandEnding.Of(_world) is { } ending)
        {
            var nobodyIsLeft = ending.Fate == BandFate.Ended;

            // The roster and the selection are about the dead, and the world under this epitaph
            // waits for a successor band: put the old band's windows away now, the way that
            // band's arrival would have (OnAnotherBandRequested), not only then.
            if (nobodyIsLeft)
            {
                _bandPanel.Visible = false;
                _selectionPanel.ClearSelection();
                _selectedPerson = null;
            }

            ShowInscription(Epitaph.Write(ending), offerAnotherBand: nobodyIsLeft);
        }
    }

    // A successor band arrives into this same world: a fresh crowd is spawned and the prologue
    // takes their place on screen. The old band's dead and graves stay where they are.
    private void OnAnotherBandRequested()
    {
        // Put away the old band's windows â€” the roster and selection are about dead people.
        _bandPanel.Visible = false;
        _selectionPanel.ClearSelection();

        // The new band has not walked this land yet â€” fog clears around their new camp.
        _world.Exploration.Reset();

        // Clear selection: the old person is dead.
        _selectedPerson = null;

        // Spawn the successor.
        var idRng = new Random(_world.Clock.CurrentTick.GetHashCode());
        var newCamp = MapLoader.SpawnNewBand(_world, idRng, _campCenter);
        _campCenter = newCamp;

        // Reset ending tracker so the new band's fate changes are announced independently.
        _endingAnnouncements = new EndingAnnouncements();

        // Brief pre-roll so the new band is not standing still behind the prologue.
        _world.Advance(IdleTask.MaxPauseTicks + 1);

        // Update the fog visuals so the area around the new camp is already revealed.
        // _Process is blocked by the inscription, so we do it here instead of waiting.
        _presenter.RefreshExploration(_cameraRig.RigGlobalPosition, _cameraRig.ViewRadius);
        _fogOfWar.Refresh();
        _groundClouds.Refresh();

        var arrival = BandArrival.Of(_world);
        _bandArrivalTick = arrival.ArrivalTick;
        ShowInscription(Prologue.Write(arrival), offerAnotherBand: false);

        // Move the camera to the new camp.
        var campX = (float)newCamp.X;
        var campZ = (float)newCamp.Y;
        var campHeight = _terrain.SampleHeight(campX, campZ);
        _cameraRig.FocusOn(new Vector3(campX, campHeight, campZ));
    }

    // Every inscription stops the clock until dismissed (see _Process); its title goes up on
    // the overlay and the whole of it into the chronicle, where it stays for the session.
    private void ShowInscription(Inscription inscription, bool offerAnotherBand)
    {
        RecordInscription(inscription);
        _inscriptionOverlay.Show(inscription, offerAnotherBand);
    }

    // Written down without stopping anything. For a moment the player is already living
    // through - they have just typed the name themselves - taking the whole screen to tell them
    // what they did would be ceremony in the way of play. The chronicle keeps it either way,
    // and that is what outlives the band.
    private void RecordInscription(Inscription inscription)
    {
        _chronicle.Add(inscription);
        _statusBar.ShowChronicleButton();
        GD.Print($"Inscription: {inscription.Title}");
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

    // Debug only: the world's raw numbers and the levers that move them. What the player is meant
    // to read and press lives in SelectionPanel; this window keeps the dump, the spawner, the
    // extinguisher and the map reveal, none of which belong in the game proper (docs/todo/todo.md).
    //
    // Shut until the status bar's Inspector button is pressed. It used to open with the game and
    // sit over the corner the band's roster now claims, which put a debug tool in front of the
    // player before they had asked for one.
    private void SetUpInspectorWindow(CanvasLayer canvas)
    {
        const float width = 340f;

        var panel = new FloatingPanel("Inspector (debug)")
        {
            Position = new Vector2(16, 16),
            Visible = false,
            CustomMinimumSize = new Vector2(width, 0),
            // A Theme resource cascades its DefaultFontSize down to every descendant Control that
            // doesn't set its own override - unlike AddThemeFontSizeOverride, which only affects
            // the single Control it's called on.
            Theme = new Theme { DefaultFontSize = _presentation.InspectorFontSize },
        };
        canvas.AddChild(panel);
        _inspector = panel;
        _statusBar.InspectorRequested += () => _inspector.Visible = !_inspector.Visible;

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

        // The quick way to the epitaph and its "Another band comes" offer (docs/todo/todo.md):
        // the epitaph of a band nobody is left in carries no closing words, so without this the
        // only way to that screen is playing the band out by hand.
        var extinguishButton = new Button { Text = "Extinguish Band" };
        extinguishButton.Pressed += OnExtinguishButtonPressed;
        panel.Body.AddChild(extinguishButton);

        // A development view, not a gameplay one (see RevealableExploration): the whole map as if
        // fog of war did not exist.
        var revealMapToggle = new CheckButton { Text = "Reveal Map" };
        revealMapToggle.Toggled += OnRevealMapToggled;
        panel.Body.AddChild(revealMapToggle);

        _buildingsLabel = new Label { Text = "Buildings: none" };
        panel.Body.AddChild(_buildingsLabel);

        _gravesLabel = new Label { Text = "Graves: none" };
        panel.Body.AddChild(_gravesLabel);
    }

    // The player's panel, against the opposite edge from the debug inspector so both can be open.
    private void SetUpSelectionPanel(CanvasLayer canvas)
    {
        _selectionPanel = new SelectionPanel();
        _selectionPanel.ActionInvoked += OnActionInvoked;
        _selectionPanel.PackRequested += OpenWorkshop;
        _selectionPanel.CloseRequested += ClearSelection;
        canvas.AddChild(_selectionPanel);
    }

    private void SetUpStatusBar(CanvasLayer canvas)
    {
        _statusBar = new StatusBar();
        _statusBar.AddThemeStyleboxOverride("panel", PanelChrome.Background());
        canvas.AddChild(_statusBar);
        _statusBar.SetTick(_world.Clock.CurrentTick, _world.CurrentSeason);
    }

    // Refreshes right away rather than waiting for the next tick: the tick interval is long
    // enough that a toggle which only took effect a moment later would read as broken.
    private void OnRevealMapToggled(bool toggledOn)
    {
        _exploration.RevealAll = toggledOn;
        _presenter.RefreshExploration(_cameraRig.RigGlobalPosition, _cameraRig.ViewRadius);
        _fogOfWar.Refresh();
        _groundClouds.Refresh();
    }

    private void OnSpawnButtonPressed()
    {
        var name = _world.GenerateUnrelatedName(Random.Shared);
        _world.Execute(new SpawnPersonCommand(name, FindFreeSpawnPosition(), Person.Unknown, Person.Unknown));
    }

    private void OnExtinguishButtonPressed()
    {
        _world.Execute(new ExtinguishBandCommand());
    }

    // A line pressed on the selected person's card or on the contextual menu. Both draw offers
    // for whoever is selected, so that is who carries it out.
    private void OnActionInvoked(ActionOffer offer)
    {
        if (_selectedPerson is not { } person)
        {
            return;
        }

        _contextMenu.Close();
        Perform(person, offer);
    }

    // Every action the player asks for arrives here - pressed on a card, picked off the menu, or
    // meant by a left click on something in the world. The offer carries both the command and the
    // world's own answer about whether it can run (see ActionOffer), so nothing is re-checked
    // here: the handlers this replaced each re-asked a different subset and worded the refusal
    // their own way.
    private void Perform(Person person, ActionOffer offer)
    {
        if (!offer.IsAvailable)
        {
            return;
        }

        // Nobody starts knowing anything (see SkillDefinition.BaseTechnique): being directed is how
        // a person is shown the way, so an action that teaches grants its base technique first.
        // Granted when the order is given rather than when it is carried out, so somebody sent off
        // to a tree already knows what to do with it by the time they get there.
        if (offer.TeachFirst is { } skill)
        {
            TeachBaseTechniqueIfNeeded(person, skill);
        }

        if (offer.NeedsWalkingTo && offer.Target is { } target)
        {
            _pendingOrders.Add(person, offer);
            // Fully qualified: inside a Node3D, a bare `Position` is the node's own Vector3.
            _world.Execute(new MoveCommand(person, Core.World.Position.Approach(person.Position, target, _presentation.ApproachDistance)));
        }
        else
        {
            // A new order replaces whatever they were on their way to do - including a plain walk,
            // which is the player changing their mind.
            _pendingOrders.Forget(person);
            Execute(offer.Command);
        }

        RefreshSelection();
        RefreshBuildingsLabel();
        RefreshGravesLabel();
    }

    // Two commands take something off the map, and views are pushed to the presenter rather than
    // reconciled from world state, so both have to say so. The per-tick sweep would catch a felled
    // node a moment later; a buried person it would never catch at all.
    private void Execute(ICommand command)
    {
        _world.Execute(command);

        switch (command)
        {
            case FellCommand fell:
                _presenter.RemoveResourceNodeView(fell.Node.Id);
                break;
            case BuryCommand bury:
                _presenter.RemovePersonView(bury.Deceased.Id);
                break;
        }
    }

    private Position FindFreeSpawnPosition()
    {
        const float minDistance = 1.2f;
        const float spread = 16f;

        return FreePositionSearch.Find(
            () => new Position(
                _campCenter.X + ((GD.Randf() - 0.5f) * spread),
                _campCenter.Y + ((GD.Randf() - 0.5f) * spread)),
            candidate => !_world.People.Any(p => WorldState.Distance(p.Position, candidate) < minDistance),
            maxAttempts: 20);
    }

    // The few people this one is closest to. Bonds never formed are absent (see Affections.For),
    // so a loner reads "none" rather than a column of zeroes.
    private string BondsText(Person person)
    {
        var namesById = _world.People.ToDictionary(p => p.Id, p => p.Name);
        var bonds = _world.Affections.For(person.Id)
            .Where(bond => namesById.ContainsKey(bond.Other))
            .Take(3)
            .Select(bond => $"{namesById[bond.Other]} {bond.Value:0}")
            .ToList();

        return bonds.Count > 0 ? string.Join(", ", bonds) : "none";
    }

    private void OnPersonClicked(Person person, MouseButton button)
    {
        if (button == MouseButton.Right)
        {
            _pointedAt = actor => TargetActions.For(_world, actor, person);
            return;
        }

        _selectedPerson = person;
        _selectedGrave = null;
        RefreshSelection();
    }

    // The player put their own card away with the cross in its corner: nobody is selected any
    // more, so the card comes down with the selection rather than on its own.
    private void ClearSelection()
    {
        _selectedPerson = null;
        _selectedGrave = null;
        RefreshSelection();
    }

    private void OnGraveSelected(Grave grave)
    {
        _selectedGrave = grave;
        _selectedPerson = null;
        RefreshSelection();
    }

    // A left click on a resource is the one shortcut kept from before there was a menu: "gather
    // that" is the only thing anybody means by pointing at a bush, and it is how the game is
    // played. Everything else aimed at a target is asked for by name, on the right button.
    //
    // Depleting a node to zero keeps its view - the plant is still there, fruitless until
    // RegenPerTick refills it. Only IsAlive turning false (felled or withered) removes it.
    private void OnResourceNodeClicked(Entity node, MouseButton button)
    {
        if (button == MouseButton.Right)
        {
            _pointedAt = actor => TargetActions.For(_world, actor, node);
            return;
        }

        if (Acting() is { } person)
        {
            Perform(person, TargetActions.Gather(_world, person, node));
        }
    }

    // A left click on a pile is the one shortcut kept, the same as a resource's Gather: "pick
    // that up" is the only thing anybody means by pointing at it.
    private void OnItemPileClicked(Entity pile, MouseButton button)
    {
        if (button == MouseButton.Right)
        {
            _pointedAt = actor => TargetActions.For(_world, actor, pile);
            return;
        }

        if (Acting() is { } person)
        {
            Perform(person, TargetActions.PickUp(_world, person, pile));
        }
    }

    // Either button opens the store's menu: a hut has no one obvious thing to do with it, so
    // putting something in, taking something out and mending it are equally the point.
    private void OnBuildingClicked(Entity building, MouseButton button)
    {
        _pointedAt = actor => TargetActions.For(_world, actor, building);

        if (button == MouseButton.Left)
        {
            ShowContextMenu(GetViewport().GetMousePosition());
        }
    }

    // Fires the order of everyone who has arrived where they were sent (see PendingOrders).
    private void ResolvePendingOrders()
    {
        foreach (var offer in _pendingOrders.Ready(_world))
        {
            Execute(offer.Command);
        }

        foreach (var failed in _pendingOrders.Failed)
        {
            _statusBar.Notify($"{failed.Person.Name} arrived too late to {failed.Label.ToLowerInvariant()}.");
        }
    }

    // Nobody starts knowing anything (see SkillDefinition.BaseTechnique): the player directing an
    // action is how the person is shown the way, so every player-driven action grants its base
    // technique first rather than silently no-oping.
    private void TeachBaseTechniqueIfNeeded(Person person, SkillTypeId skill)
    {
        var baseTechnique = _world.Configuration.SkillCatalog.Get(skill).BaseTechnique;
        if (!person.KnownTechniques.Contains(baseTechnique))
        {
            _world.Execute(new GrantTechniqueCommand(person, baseTechnique));
        }
    }

    // The view has already tried HoverRescue.TryClickElsewhere (a full re-cast of the ray past
    // everything ruled out) before forwarding here, so this genuinely is a ground click.
    //
    // The position handed over is the ray's hit on the view's collision box - up in the air on a
    // tree-sized box's front face, tens of meters off the ground under the cursor (see
    // GroundPick) - so only the screen position is reused and the ground re-derived. A click
    // that finds no ground (sky past the terrain's edge) is dropped rather than guessed.
    private void OnMissedClick(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (camera is not Camera3D camera3D
            || @event is not InputEventMouseButton { Pressed: true } mouseButton)
        {
            return;
        }

        if (GroundPick.FindGround(camera3D, mouseButton.Position) is { } groundPosition)
        {
            OnGroundClicked(groundPosition, mouseButton.ButtonIndex);
        }
    }

    private void OnGroundInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx)
    {
        if (@event is InputEventMouseButton { Pressed: true } mouseButton)
        {
            OnGroundClicked(position, mouseButton.ButtonIndex);
        }
    }

    // Left means walk there; right asks what else could be done on that spot, which is where
    // building belongs - it needs a place chosen rather than a thing pointed at.
    //
    // Reached straight from the terrain's own collider as well as from a view that declined the
    // click, so the wheel has to be turned away here too (see OrderButtons).
    private void OnGroundClicked(Vector3 groundPosition, MouseButton button)
    {
        if (!OrderButtons.Includes(button))
        {
            return;
        }

        var ground = WorldSpace.ToSimulation(groundPosition);

        if (button == MouseButton.Right)
        {
            _pointedAt = actor => TargetActions.For(_world, actor, ground);
            return;
        }

        if (button == MouseButton.Left && Acting() is { } person)
        {
            Perform(person, TargetActions.WalkTo(_world, person, ground));
        }
    }

    // Everything on screen that is about people: the player's panel for whoever is selected,
    // behind it the debug inspector's raw dump of the same person, and the band's roster, whose
    // lines go stale on exactly the same occasions.
    private void RefreshSelection()
    {
        RefreshInfoLabel();
        RefreshBandPanel();

        if (_selectedGrave is { } grave)
        {
            _selectionPanel.ShowGrave(InspectorText.ForGraveRecord(grave, _world.Configuration.SkillCatalog));
            return;
        }

        if (_selectedPerson is { } person)
        {
            _selectionPanel.ShowPerson(SelectionCard.For(_world, person), PersonActions.For(_world, person));
            return;
        }

        _selectionPanel.ClearSelection();
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
            $"Age: {AgeText(person)} ({_world.LifeStageOf(person)}, {person.Sex})\n" +
            $"Task: {InspectorText.ForTask(person)}\n" +
            $"Hunger: {person.Needs.Hunger}  Fatigue: {person.Needs.Fatigue}\n" +
            $"Skills: {skills}\n" +
            $"Known techniques: {techniques}\n" +
            $"Closest to: {BondsText(person)}\n" +
            $"Carrying: {carriedWeight}/{maxCarryWeight}\n" +
            $"Inventory: {inventory}";
    }

    private void RefreshBuildingsLabel()
    {
        var buildings = _world.Entities.Where(e => e.Category == EntityCategory.Building).ToList();
        _buildingsLabel.Text = "Buildings: " + (buildings.Count > 0
            ? string.Join(", ", buildings.Select(BuildingSummary))
            : "none");
    }

    private string AgeText(Person person) =>
        DurationText.For(_world.Clock.CurrentTick - person.BirthTick, _world.Configuration.Rules.TicksPerYear, _world.Configuration.Rules.TicksPerSeason);

    private void RefreshGravesLabel()
    {
        _gravesLabel.Text = $"Graves: {_world.Graves.Count}";
    }

    private static string BuildingSummary(Entity building)
    {
        var inventory = building.Storage!.Counts.Count > 0
            ? string.Join(", ", building.Storage.Counts.Select(kv => $"{kv.Key} x{kv.Value}"))
            : "empty";
        return $"{building.Kind} #{building.Id} ({building.Condition!.Value:0}%) [{inventory}]";
    }
}
