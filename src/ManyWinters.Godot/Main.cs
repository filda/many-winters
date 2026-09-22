using Godot;
using ManyWinters.Core.Commands;
using ManyWinters.Core.Continuity;
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

namespace ManyWinters.Godot;

public partial class Main : Node3D
{
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

    private SelectionController _selection = null!;
    private WorkshopController _workshopController = null!;
    private MainUi _mainUi = null!;
    private WorldInputController _worldInput = null!;
    private BandContinuityController _continuity = null!;
    private WorldFrameUpdater _worldFrameUpdater = null!;
    private SimulationLoop _simulationLoop = null!;
    private OcclusionFader _occlusionFader = null!;
    private OrderCoordinator _orderCoordinator = null!;

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
        var campCenter = map.CampCenter;
        GetViewport().PhysicsObjectPicking = true;
        SetUpLighting();
        SetUpSky();

        await Building(55, "Laying the ground");
        SetUpTerrain();

        await Building(70, "Placing the camera");
        SetUpCamera(campCenter);

        await Building(75, "Drawing the pages");
        // Built ahead of the UI rather than with the rest of the band below: SetUpUi constructs
        // the workshop controller, which needs the order coordinator (and, through it, the
        // presenter) already in hand.
        _presenter = new WorldPresenter(this, _world, _exploration, _cameraRig.RigGlobalPosition, _cameraRig.ViewRadius, OnPersonClicked, OnResourceNodeClicked, OnBuildingClicked, OnGraveSelected, OnItemPileClicked, OnMissedClick, _terrain.SampleHeight);
        _occlusionFader = new OcclusionFader(_cameraRig, _presenter, _presentation);
        _orderCoordinator = new OrderCoordinator(_world, _presenter, _presentation);
        SetUpUi();
        _orderCoordinator.WorldChanged += OnOrderCoordinatorWorldChanged;
        _orderCoordinator.OrderFailed += _mainUi.StatusBar.Notify;

        await Building(85, "Gathering the clouds");
        CloudScatter.Scatter(this, _terrain.Half);
        _cloudFogMask = new CloudFogMask(this, _cameraRig.Camera);
        _worldFrameUpdater = new WorldFrameUpdater(_cameraRig, _occlusionFader, _selection, _presenter, _cloudFogMask);

        await Building(90, "Setting out the band");
        _fogOfWar = new FogOfWarRenderer(_exploration, _terrain.Half, _cameraRig.Camera, _cloudFogMask);
        _groundClouds = new GroundClouds(this, _fogOfWar, _terrain.Half, _terrain.SampleHeight);
        _continuity = new BandContinuityController(_world, campCenter, _presenter, _fogOfWar, _groundClouds, _cameraRig, _terrain, _mainUi, _selection, _workshopController);
        _simulationLoop = new SimulationLoop(_world, _pacing, _presenter, _cameraRig, _fogOfWar, _groundClouds, _orderCoordinator, _mainUi, _selection, _continuity);

        await Building(100, "The band arrives");

        _loadingCanvas.QueueFree();
        _loadingCanvas = null;
        _loadingScreen = null;
        _loading = false;

        _continuity.ShowInitialArrival();

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

        _worldFrameUpdater.Update((float)delta);
        _simulationLoop.Update(delta);
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
            MainUi.ToggleFullscreen();
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
            _worldInput.CloseContextMenu();
            _workshopController.HandleEscape();
            _mainUi.HandleEscape();
            _selection.CloseDetail();
        }

        _worldInput.Handle(@event, GetViewport());
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

        _worldInput.HandleUnhandled(@event, GetViewport());
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

    private void SetUpCamera(Position campCenter)
    {
        var campX = (float)campCenter.X;
        var campZ = (float)campCenter.Y;
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
        _mainUi = new MainUi(this, _world, _presentation);

        SetUpSelectionController(_mainUi.Canvas);
        // Opposite the inspector, so the two can be open at once without covering each other.
        _mainUi.AttachChronicle();
        SetUpWorkshopController(_mainUi.Canvas);
        // Attached rather than built with the rest of selection above: the detail page has to
        // land after the workbench in the canvas so it draws on top of it.
        _selection.AttachDetailPanel(_mainUi.Canvas);
        _selection.WorkshopRequested += _workshopController.Toggle;
        SetUpWorldInputController(_mainUi.Canvas);
        // Last: inscriptions and the pause/help pages draw over everything else built above.
        _mainUi.AttachOverlaysAndPauseHelp();
        // Letting a clock-holding page this type owns go primes the tick accumulator, so the
        // world starts again on the next frame rather than a full interval later.
        _mainUi.ClockShouldResume += TickAsSoonAsPossible;

        // The workbench and the detail page keep owning their own clock-holding/pause-blocking
        // registration, since MainUi never reaches into controls it does not itself construct.
        _mainUi.RegisterModal(_workshopController.ModalControl, holdsClock: true, blocksPause: true);
        _mainUi.RegisterModal(_selection.DetailModalControl, holdsClock: true, blocksPause: true);

        _mainUi.Inspector.SpawnRequested += OnSpawnButtonPressed;
        _mainUi.Inspector.ExtinguishRequested += OnExtinguishButtonPressed;
        _mainUi.Inspector.RevealMapToggled += OnRevealMapToggled;
    }

    private void SetUpSelectionController(CanvasLayer canvas)
    {
        _selection = new SelectionController(canvas, _world, _presenter, _cameraRig, _presentation);
        _selection.Refreshed += RefreshInfoLabel;
        // Letting the detail page go primes the tick accumulator, so the world starts again on
        // the next frame rather than a full interval later - as dismissing the controls page does.
        _selection.Closed += TickAsSoonAsPossible;
        _mainUi.StatusBar.BandRequested += _selection.ToggleBandPanel;
    }

    // The workbench, opened from the pack line on the selected person's card. Like the pause
    // page it holds the clock while it is up (see _Process): working a thing over is meant to be
    // unhurried.
    private void SetUpWorkshopController(CanvasLayer canvas)
    {
        _workshopController = new WorkshopController(canvas, _world, _orderCoordinator);
        // Letting the workbench go primes the tick accumulator, so the world starts again on the
        // next frame rather than a full interval later - as dismissing the controls page does.
        _workshopController.Closed += TickAsSoonAsPossible;
    }

    // After the windows, so a menu opened over one of them is on top of it; before the
    // inscription overlay and the pause panel, which are on top of everything.
    private void SetUpWorldInputController(CanvasLayer canvas)
    {
        _worldInput = new WorldInputController(canvas, _world, _cameraRig, _selection, _orderCoordinator, _mainUi.StatusBar, _presentation);
        // Presenter is already built (see _Ready) by the time this runs; the two-step handoff
        // exists for the construction cycle - the presenter is itself built with this
        // controller's click callbacks - not because the presenter is unready here.
        _worldInput.AttachPresenter(_presenter);
        _selection.ActionInvoked += _worldInput.PerformAction;
    }

    // Space toggles the clock at the player's request - ignored while an inscription holds it,
    // which is not the player's to override.
    private void TogglePause()
    {
        var band = BandArrival.Of(_world);
        var sinceArrival = DurationText.For(_world.Clock.CurrentTick - _continuity.ArrivalTick, _world.Configuration.Rules.TicksPerYear, _world.Configuration.Rules.TicksPerSeason);
        _mainUi.TogglePause(band.BandName, sinceArrival, PopulationSummary.Of(band.People, band.Men, band.Women, band.Children));
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

    // What every caller of OrderCoordinator.Perform used to refresh by hand once it had executed
    // or queued the offer.
    private void OnOrderCoordinatorWorldChanged()
    {
        _selection.Refresh();
        _simulationLoop.RefreshBuildingsLabel();
        _simulationLoop.RefreshGravesLabel();
    }

    // Wired to every clock-holding page's own dismiss/close/resume signal (see SetUpUi,
    // SetUpSelectionController, SetUpWorkshopController): _simulationLoop does not exist yet
    // when those signals are first subscribed, so this reads it lazily at call time instead of
    // being passed as a callback directly.
    private void TickAsSoonAsPossible() => _simulationLoop.TickAsSoonAsPossible();

    private Position FindFreeSpawnPosition()
    {
        const float minDistance = 1.2f;
        const float spread = 16f;

        return FreePositionSearch.Find(
            () => new Position(
                _continuity.CampCenter.X + ((GD.Randf() - 0.5f) * spread),
                _continuity.CampCenter.Y + ((GD.Randf() - 0.5f) * spread)),
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

    // Passed to WorldPresenter/TerrainSetup as click callbacks before WorldInputController can
    // exist (it is built inside SetUpUi, from SelectionController and OrderCoordinator - see
    // _Ready), so these forward instead of being the controller's own methods directly. Each
    // reads _worldInput lazily at call time, long after _Ready has finished building it.
    private void OnPersonClicked(Person person, MouseButton button) => _worldInput.OnPersonClicked(person, button);

    private void OnGraveSelected(Grave grave) => _worldInput.OnGraveSelected(grave);

    private void OnResourceNodeClicked(Entity node, MouseButton button) => _worldInput.OnResourceNodeClicked(node, button);

    private void OnItemPileClicked(Entity pile, MouseButton button) => _worldInput.OnItemPileClicked(pile, button);

    private void OnBuildingClicked(Entity building, MouseButton button) => _worldInput.OnBuildingClicked(building, button);

    private void OnMissedClick(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx) =>
        _worldInput.OnMissedClick(camera, @event, position, normal, shapeIdx);

    private void OnGroundInputEvent(Node camera, InputEvent @event, Vector3 position, Vector3 normal, long shapeIdx) =>
        _worldInput.OnGroundInputEvent(camera, @event, position, normal, shapeIdx);

    // The debug inspector's raw dump of whoever is selected - kept apart from the player-facing
    // panels, which are SelectionController's own to refresh.
    private void RefreshInfoLabel()
    {
        if (_selection.Grave is { } grave)
        {
            _mainUi.Inspector.ShowInfo(InspectorText.ForGrave(grave));
            return;
        }

        if (_selection.Person is not { } person)
        {
            _mainUi.Inspector.ShowInfo("No selection.");
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
        _mainUi.Inspector.ShowInfo(
            $"{person.Id}  {person.Name}{status}\n" +
            $"Position: {person.Position}\n" +
            $"Age: {AgeText(person)} ({_world.LifeStageOf(person)}, {person.Sex})\n" +
            $"Task: {InspectorText.ForTask(person)}\n" +
            $"Hunger: {person.Needs.Hunger}  Fatigue: {person.Needs.Fatigue}\n" +
            $"Skills: {skills}\n" +
            $"Known techniques: {techniques}\n" +
            $"Closest to: {BondsText(person)}\n" +
            $"Carrying: {carriedWeight}/{maxCarryWeight}\n" +
            $"Inventory: {inventory}");
    }

    private string AgeText(Person person) =>
        DurationText.For(_world.Clock.CurrentTick - person.BirthTick, _world.Configuration.Rules.TicksPerYear, _world.Configuration.Rules.TicksPerSeason);
}
